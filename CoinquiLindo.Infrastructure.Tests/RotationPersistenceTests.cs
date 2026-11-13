using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class RotationPersistenceTests : IDisposable
    {
        private static readonly DateOnly Monday = new(2026, 11, 2);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public RotationPersistenceTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var context = CreateContext();
            context.Database.Migrate();
        }

        [Fact]
        public async Task RotationPlan_Should_RoundTripTheChosenOrder_AndPersistUpdates()
        {
            var house = await SeedHouseAsync(tenants: 3, rooms: 2);
            var tenants = house.Tenants.Select(t => t.Id).Reverse().ToList();
            var rooms = house.Rooms.Select(r => r.Id).Reverse().ToList();
            var plan = new RotationPlan(house, tenants, rooms, Monday);

            await using (var context = CreateContext())
            {
                await new RotationPlanRepository(context).AddAsync(plan);
            }

            await using (var context = CreateContext())
            {
                var repository = new RotationPlanRepository(context);
                var loaded = await repository.GetByHouseIdAsync(house.Id);
                loaded!.Id.Should().Be(plan.Id);
                loaded.StartDate.Should().Be(Monday);
                loaded.TenantIds.Should().Equal(tenants);
                loaded.RoomIds.Should().Equal(rooms);

                var persistedHouse = await new HouseRepository(context).GetByIdAsync(house.Id);
                loaded.Update(persistedHouse!, [tenants[1]], [rooms[1], rooms[0]], Monday.AddDays(7));
                await repository.UpdateAsync(loaded);
            }

            await using (var context = CreateContext())
            {
                var updated = (await new RotationPlanRepository(context).GetAllAsync()).Single();
                updated.TenantIds.Should().Equal(tenants[1]);
                updated.RoomIds.Should().Equal(rooms[1], rooms[0]);
                updated.StartDate.Should().Be(Monday.AddDays(7));
            }

            await using (var context = CreateContext())
            {
                var repository = new RotationPlanRepository(context);
                await repository.RemoveAsync((await repository.GetByHouseIdAsync(house.Id))!);
            }

            await using (var context = CreateContext())
            {
                (await new RotationPlanRepository(context).GetByHouseIdAsync(house.Id)).Should().BeNull();
            }
        }

        [Fact]
        public async Task RotationPlan_Should_BeUniquePerHouse()
        {
            var house = await SeedHouseAsync(tenants: 1, rooms: 1);

            await using (var context = CreateContext())
            {
                await new RotationPlanRepository(context).AddAsync(Plan(house));
            }

            await using var secondContext = CreateContext();
            var second = () => new RotationPlanRepository(secondContext).AddAsync(Plan(house));

            await second.Should().ThrowAsync<DbUpdateException>();
        }

        [Fact]
        public async Task Shift_Should_PersistRotationPlanAndHelper()
        {
            var house = await SeedHouseAsync(tenants: 2, rooms: 1);
            var planId = Guid.NewGuid();
            Guid shiftId;

            await using (var context = CreateContext())
            {
                var persisted = (await new HouseRepository(context).GetByIdAsync(house.Id))!;
                // By name: the order of the tenants loaded from the database is not guaranteed
                var shift = NewShift(persisted, persisted.Tenants.Single(t => t.Name == "Utente 1"), Monday, planId);
                shift.SetHelper(shift.AssignedTenant.Id, persisted.Tenants.Single(t => t.Name == "Utente 2"));
                await new ShiftRepository(context).AddAsync(shift);
                shiftId = shift.Id;
            }

            await using (var context = CreateContext())
            {
                var byId = await new ShiftRepository(context).GetByIdAsync(house.Id, shiftId);
                var byHouse = (await new ShiftRepository(context).GetByHouseIdAsync(house.Id)).Single();

                byId!.RotationPlanId.Should().Be(planId);
                byId.HelperTenant!.Id.Should().Be(house.Tenants.Single(t => t.Name == "Utente 2").Id);
                byHouse.HelperTenant!.Name.Should().Be("Utente 2");

                byId.RemoveHelper(byId.AssignedTenant.Id);
                await new ShiftRepository(context).UpdateAsync(byId);
            }

            await using (var context = CreateContext())
            {
                (await new ShiftRepository(context).GetByIdAsync(house.Id, shiftId))!.HelperTenant.Should().BeNull();
            }
        }

        [Fact]
        public async Task TryAddRange_Should_RejectDuplicatedRotationShifts_WithoutSavingAnyOfThem()
        {
            var house = await SeedHouseAsync(tenants: 2, rooms: 2);
            var planId = Guid.NewGuid();

            await using (var context = CreateContext())
            {
                var persisted = (await new HouseRepository(context).GetByIdAsync(house.Id))!;
                (await new ShiftRepository(context).TryAddRangeAsync([NewShift(persisted, persisted.Tenants.First(), Monday, planId)]))
                    .Should().BeTrue();
            }

            await using (var context = CreateContext())
            {
                var persisted = (await new HouseRepository(context).GetByIdAsync(house.Id))!;
                var repository = new ShiftRepository(context);
                var duplicate = NewShift(persisted, persisted.Tenants.Last(), Monday, planId);
                var otherRoom = new Shift(persisted.Id, persisted.Tenants.First(), persisted.Rooms.OrderBy(r => r.Name).Last(),
                    Monday, Monday.AddDays(6), persisted.GetTimeZone(), planId);

                (await repository.TryAddRangeAsync([otherRoom, duplicate])).Should().BeFalse();

                // The context stays usable after the rejected batch
                (await repository.TryAddRangeAsync([NewShift(persisted, persisted.Tenants.First(), Monday.AddDays(7), planId)]))
                    .Should().BeTrue();
            }

            await using (var context = CreateContext())
            {
                (await new ShiftRepository(context).GetByHouseIdAsync(house.Id)).Select(s => s.StartDate)
                    .Should().BeEquivalentTo([Monday, Monday.AddDays(7)]);
            }
        }

        [Fact]
        public async Task ManualShifts_Should_NotCollideWithEachOther()
        {
            var house = await SeedHouseAsync(tenants: 1, rooms: 1);

            await using var context = CreateContext();
            var persisted = (await new HouseRepository(context).GetByIdAsync(house.Id))!;
            var repository = new ShiftRepository(context);

            (await repository.TryAddRangeAsync([NewShift(persisted, persisted.Tenants.Single(), Monday, null)])).Should().BeTrue();
            (await repository.TryAddRangeAsync([NewShift(persisted, persisted.Tenants.Single(), Monday, null)])).Should().BeTrue();
        }

        [Fact]
        public async Task GetRotationShifts_Should_ReturnRotationShiftsOfTheHouseNotEndedYet_AndRemoveRangeShouldDeleteThem()
        {
            var house = await SeedHouseAsync(tenants: 1, rooms: 1);
            var planId = Guid.NewGuid();

            await using (var context = CreateContext())
            {
                var persisted = (await new HouseRepository(context).GetByIdAsync(house.Id))!;
                var tenant = persisted.Tenants.Single();
                await new ShiftRepository(context).TryAddRangeAsync([
                    NewShift(persisted, tenant, Monday.AddDays(-7), planId),
                    NewShift(persisted, tenant, Monday, planId),
                    NewShift(persisted, tenant, Monday.AddDays(7), planId),
                    NewShift(persisted, tenant, Monday.AddDays(7), Guid.NewGuid()),
                    NewShift(persisted, tenant, Monday.AddDays(7), null)]);
            }

            await using (var context = CreateContext())
            {
                var repository = new ShiftRepository(context);
                // Sunday 08/11 is the last day of the week starting on Monday 02/11
                var shifts = await repository.GetRotationShiftsAsync(house.Id, Monday.AddDays(6));

                // Any plan of the house (also a deleted one), never manual shifts
                shifts.Select(s => s.StartDate).Should().BeEquivalentTo([Monday, Monday.AddDays(7), Monday.AddDays(7)]);
                shifts.Should().OnlyContain(s => s.Checklist.Count == 1 && s.AssignedTenant != null && s.AssignedRoom != null);
                (await repository.GetRotationShiftsAsync(Guid.NewGuid(), Monday)).Should().BeEmpty();

                await repository.RemoveRangeAsync(shifts.Where(s => s.StartDate > Monday).ToList());
            }

            await using (var context = CreateContext())
            {
                (await new ShiftRepository(context).GetRotationShiftsAsync(house.Id, Monday.AddDays(-30)))
                    .Select(s => s.StartDate).Should().BeEquivalentTo([Monday.AddDays(-7), Monday]);
                (await new ShiftRepository(context).GetByHouseIdAsync(house.Id)).Should().HaveCount(3);
            }
        }

        private static RotationPlan Plan(House house) =>
            new(house, house.Tenants.Select(t => t.Id), house.Rooms.Select(r => r.Id), Monday);

        private static Shift NewShift(House house, Tenant tenant, DateOnly start, Guid? planId) =>
            new(house.Id, tenant, house.Rooms.OrderBy(r => r.Name).First(), start, start.AddDays(6), house.GetTimeZone(), planId);

        private async Task<House> SeedHouseAsync(int tenants, int rooms)
        {
            var house = new House("Casa rotazione");
            for (var i = 1; i <= tenants; i++)
            {
                house.AddTenant(new Tenant($"Utente {i}"));
            }

            for (var i = 1; i <= rooms; i++)
            {
                var room = new Room($"Stanza {i}");
                room.AddCleaningTask(new CleaningTask($"Pulire stanza {i}"));
                house.AddRoom(room);
            }

            await using var context = CreateContext();
            await new HouseRepository(context).AddAsync(house);
            return house;
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
