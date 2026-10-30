using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class ShiftRepositoryTests : IDisposable
    {
        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public ShiftRepositoryTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var context = CreateContext();
            context.Database.Migrate();
        }

        [Fact]
        public async Task GetByHouseIdAsync_Should_ReturnShiftsWithTenantRoomAndChecklist()
        {
            var (houseId, shiftId, checklistItemId) = await SeedShiftAsync();

            await using var context = CreateContext();
            var shifts = await new ShiftRepository(context).GetByHouseIdAsync(houseId);

            shifts.Should().ContainSingle();
            var shift = shifts.Single();
            shift.Id.Should().Be(shiftId);
            shift.HouseId.Should().Be(houseId);
            shift.AssignedTenant.Name.Should().Be("Salvo");
            shift.AssignedRoom.Name.Should().Be("Bagno");
            shift.Checklist.Should().ContainSingle(item =>
                item.Id == checklistItemId && item.Description == "Lavare il pavimento");
        }

        [Fact]
        public async Task GetByIdAsync_Should_ReturnNull_When_ShiftBelongsToAnotherHouse()
        {
            var (_, shiftId, _) = await SeedShiftAsync();

            await using var context = CreateContext();
            var shift = await new ShiftRepository(context).GetByIdAsync(Guid.NewGuid(), shiftId);

            shift.Should().BeNull();
        }

        [Fact]
        public async Task AddHouseIdMigration_Should_BackfillExistingShiftsFromTheirRoomHouse()
        {
            var house = new House("Casa preesistente");
            var tenant = new Tenant("Salvo");
            house.AddTenant(tenant);
            var room = new Room("Bagno");
            house.AddRoom(room);
            var shiftId = Guid.NewGuid();
            var startDate = DateTime.UtcNow.Date;
            var endDate = startDate.AddDays(7);

            await using (var context = CreateContext())
            {
                var previousMigration = context.Database.GetMigrations()
                    .Single(migration => migration.EndsWith("_AddHouseInviteCode", StringComparison.Ordinal));
                await context.Database.MigrateAsync(previousMigration);
                await new HouseRepository(context).AddAsync(house);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Shifts" ("Id", "AssignedRoomId", "AssignedTenantId", "StartDate", "EndDate")
                    VALUES ({shiftId}, {room.Id}, {tenant.Id}, {startDate}, {endDate});
                    """);
                await context.Database.MigrateAsync();
            }

            await using var assertContext = CreateContext();
            var migratedShift = await new ShiftRepository(assertContext).GetByIdAsync(house.Id, shiftId);

            migratedShift.Should().NotBeNull();
            migratedShift!.HouseId.Should().Be(house.Id);
        }

        private async Task<(Guid HouseId, Guid ShiftId, Guid ChecklistItemId)> SeedShiftAsync()
        {
            var house = new House("Casa");
            var tenant = new Tenant("Salvo");
            house.AddTenant(tenant);
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Lavare il pavimento"));
            house.AddRoom(room);

            await using (var context = CreateContext())
            {
                await new HouseRepository(context).AddAsync(house);
            }

            await using (var context = CreateContext())
            {
                var persistedHouse = await new HouseRepository(context).GetByIdAsync(house.Id);
                var shift = new Shift(
                    persistedHouse!.Id,
                    persistedHouse.Tenants.Single(),
                    persistedHouse.Rooms.Single(),
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddDays(7));
                await new ShiftRepository(context).AddAsync(shift);
                return (house.Id, shift.Id, shift.Checklist.Single().Id);
            }
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
