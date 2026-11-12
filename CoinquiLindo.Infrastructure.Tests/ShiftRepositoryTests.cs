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
            var (houseId, roomId, tenantId, shiftId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            var startDate = new DateTime(2026, 11, 2, 0, 0, 0, DateTimeKind.Utc);
            var endDate = startDate.AddDays(7);

            await using (var context = CreateEmptyContext())
            {
                await context.Database.MigrateAsync(await LegacySchemaSeed.PreviousMigrationAsync(context, "AddShiftHouseId"));
                await LegacySchemaSeed.InsertHouseAsync(context, houseId, roomId, tenantId);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Shifts" ("Id", "AssignedRoomId", "AssignedTenantId", "StartDate", "EndDate")
                    VALUES ({shiftId}, {roomId}, {tenantId}, {startDate}, {endDate});
                    """);
                await context.Database.MigrateAsync();
            }

            await using var assertContext = CreateEmptyContext();
            var migratedShift = await new ShiftRepository(assertContext).GetByIdAsync(houseId, shiftId);

            migratedShift.Should().NotBeNull();
            migratedShift!.HouseId.Should().Be(houseId);
        }

        [Theory]
        // Old clients saved midnight UTC of the last day; other tests and tools saved arbitrary instants
        [InlineData("2026-11-02 00:00:00", "2026-11-08 00:00:00")]
        [InlineData("2026-11-02 09:15:30.1234567", "2026-11-08 18:45:10.7654321")]
        public async Task AddShiftDueDateMigration_Should_ConvertDatesToDays_AndBackfillDueDateAndHouseTimeZone(
            string legacyStart, string legacyEnd)
        {
            var (houseId, roomId, tenantId, shiftId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

            await using (var context = CreateEmptyContext())
            {
                await context.Database.MigrateAsync(
                    await LegacySchemaSeed.PreviousMigrationAsync(context, "AddShiftDueDateAndHouseTimeZone"));
                await LegacySchemaSeed.InsertHouseAsync(context, houseId, roomId, tenantId);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Shifts" ("Id", "HouseId", "AssignedRoomId", "AssignedTenantId", "StartDate", "EndDate")
                    VALUES ({shiftId}, {houseId}, {roomId}, {tenantId}, {legacyStart}, {legacyEnd});
                    """);
                await context.Database.MigrateAsync();
            }

            await using var assertContext = CreateEmptyContext();
            var house = await new HouseRepository(assertContext).GetByIdAsync(houseId);
            var shift = await new ShiftRepository(assertContext).GetByIdAsync(houseId, shiftId);

            house!.TimeZoneId.Should().Be("Europe/Rome");
            shift!.StartDate.Should().Be(new DateOnly(2026, 11, 2));
            shift.EndDate.Should().Be(new DateOnly(2026, 11, 8));
            // End of 08/11 in Rome (CET)
            shift.DueDate.Should().Be(new DateTime(2026, 11, 8, 23, 0, 0, DateTimeKind.Utc));

            // The backfilled value must be comparable with the ones written by EF
            (await new ShiftRepository(assertContext).GetDueBetweenAsync(
                new DateTime(2026, 11, 8, 22, 59, 59, DateTimeKind.Utc),
                new DateTime(2026, 11, 8, 23, 0, 0, DateTimeKind.Utc)))
                .Should().ContainSingle(s => s.Id == shiftId);
        }

        [Fact]
        public async Task Repository_Should_PersistShiftDays_AndReadDueDateAsUtc()
        {
            var (houseId, shiftId, _) = await SeedShiftAsync();

            await using var context = CreateContext();
            var shift = await new ShiftRepository(context).GetByIdAsync(houseId, shiftId);

            shift!.StartDate.Should().Be(new DateOnly(2026, 11, 2));
            shift.EndDate.Should().Be(new DateOnly(2026, 11, 8));
            shift.DueDate.Should().Be(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc));
            shift.DueDate.Kind.Should().Be(DateTimeKind.Utc);

            var house = await new HouseRepository(context).GetByIdAsync(houseId);
            house!.TimeZoneId.Should().Be("America/New_York");
        }

        private async Task<(Guid HouseId, Guid ShiftId, Guid ChecklistItemId)> SeedShiftAsync()
        {
            var house = new House("Casa", "America/New_York");
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
                    new DateOnly(2026, 11, 2),
                    new DateOnly(2026, 11, 8),
                    persistedHouse.GetTimeZone());
                await new ShiftRepository(context).AddAsync(shift);
                return (house.Id, shift.Id, shift.Checklist.Single().Id);
            }
        }

        private AppDbContext CreateContext() => new(_options);

        // Migration tests start from an empty database, not from the one migrated by the constructor
        private readonly SqliteConnection _emptyConnection = new("DataSource=:memory:");

        private AppDbContext CreateEmptyContext()
        {
            if (_emptyConnection.State != System.Data.ConnectionState.Open)
            {
                _emptyConnection.Open();
            }

            return new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_emptyConnection).Options);
        }

        public void Dispose()
        {
            _connection.Dispose();
            _emptyConnection.Dispose();
        }
    }
}
