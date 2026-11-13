using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class CleaningTaskPersistenceTests : IDisposable
    {
        private static readonly DateOnly Monday = new(2026, 11, 16);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public CleaningTaskPersistenceTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            using var context = CreateContext();
            context.Database.Migrate();
        }

        [Fact]
        public async Task CleaningTasks_Should_BeRenamedAndDeleted_WithoutLeavingOrphans()
        {
            var (houseId, roomId) = await SeedHouseAsync("Lavare i piatti", "Pulire i fornelli");

            await using (var context = CreateContext())
            {
                var house = (await new HouseRepository(context).GetByIdAsync(houseId))!;
                var room = house.Rooms.Single(r => r.Id == roomId);
                room.RenameCleaningTask(room.CleaningTasks.Single(t => t.Description == "Lavare i piatti").Id, "Lavare e asciugare");
                room.RemoveCleaningTask(room.CleaningTasks.Single(t => t.Description == "Pulire i fornelli").Id);
                room.AddCleaningTask(new CleaningTask("Pulire i vetri"));
                await new HouseRepository(context).UpdateAsync(house);
            }

            await using (var context = CreateContext())
            {
                var room = (await new HouseRepository(context).GetByIdAsync(houseId))!.Rooms.Single();
                room.CleaningTasks.Select(t => t.Description).Should().BeEquivalentTo(["Lavare e asciugare", "Pulire i vetri"]);
                (await CountAsync(context, "SELECT COUNT(*) FROM \"CleaningTasks\"")).Should().Be(2);
            }
        }

        [Fact]
        public async Task GetByRoomStartingAfter_Should_ReturnOnlyShiftsOfTheRoomNotStarted_AndPersistChecklistChanges()
        {
            var (houseId, roomId) = await SeedHouseAsync("Lavare i piatti");
            Guid futureId;

            await using (var context = CreateContext())
            {
                var house = (await new HouseRepository(context).GetByIdAsync(houseId))!;
                var room = house.Rooms.Single(r => r.Id == roomId);
                var other = new Room("Bagno");
                house.AddRoom(other);
                await new HouseRepository(context).UpdateAsync(house);

                var repository = new ShiftRepository(context);
                var future = NewShift(house, room, Monday.AddDays(1));
                await repository.AddAsync(NewShift(house, room, Monday));
                await repository.AddAsync(future);
                await repository.AddAsync(NewShift(house, other, Monday.AddDays(1)));
                futureId = future.Id;
            }

            await using (var context = CreateContext())
            {
                var repository = new ShiftRepository(context);
                var shifts = await repository.GetByRoomStartingAfterAsync(houseId, roomId, Monday);

                var future = shifts.Should().ContainSingle().Which;
                future.Id.Should().Be(futureId);
                var taskId = future.Checklist.Single().CleaningTaskId!.Value;
                (await repository.GetByRoomStartingAfterAsync(Guid.NewGuid(), roomId, Monday)).Should().BeEmpty();

                future.RenameChecklistItem(taskId, "Lavare e asciugare");
                future.AddChecklistItem(new CleaningTask("Pulire i vetri"));
                await repository.UpdateAsync(future);
            }

            await using (var context = CreateContext())
            {
                var future = (await new ShiftRepository(context).GetByIdAsync(houseId, futureId))!;
                future.Checklist.Select(i => i.Description).Should().BeEquivalentTo(["Lavare e asciugare", "Pulire i vetri"]);
                future.Checklist.Should().OnlyContain(i => i.CleaningTaskId != null);

                future.RemoveChecklistItem(future.Checklist.Single(i => i.Description == "Pulire i vetri").CleaningTaskId!.Value);
                await new ShiftRepository(context).UpdateAsync(future);
            }

            await using (var context = CreateContext())
            {
                (await new ShiftRepository(context).GetByIdAsync(houseId, futureId))!.Checklist.Should().ContainSingle();
                (await CountAsync(context, "SELECT COUNT(*) FROM \"ShiftChecklistItem\" WHERE \"ShiftId\" IS NULL")).Should().Be(0);
            }
        }

        // Regression: deleted rotation shifts left their checklist items behind with a NULL ShiftId
        [Fact]
        public async Task RemoveRange_Should_DeleteTheChecklistItemsOfTheShifts()
        {
            var (houseId, roomId) = await SeedHouseAsync("Lavare i piatti", "Pulire i fornelli");

            await using (var context = CreateContext())
            {
                var house = (await new HouseRepository(context).GetByIdAsync(houseId))!;
                await new ShiftRepository(context).TryAddRangeAsync([NewShift(house, house.Rooms.Single(), Monday, Guid.NewGuid())]);
            }

            await using (var context = CreateContext())
            {
                var repository = new ShiftRepository(context);
                await repository.RemoveRangeAsync(await repository.GetRotationShiftsAsync(houseId, Monday));
            }

            await using (var context = CreateContext())
            {
                (await CountAsync(context, "SELECT COUNT(*) FROM \"ShiftChecklistItem\"")).Should().Be(0);
            }
        }

        [Fact]
        public async Task CleaningTaskEditingMigration_Should_DeleteOrphans_AndLinkChecklistItemsToTheTasksOfTheirRoom()
        {
            var ids = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();
            var (houseId, kitchenId, bathId, tenantId, shiftId, washTaskId, bathWashTaskId, orphanTaskId, linkedItemId, unmatchedItemId, orphanItemId) =
                (ids[0], ids[1], ids[2], ids[3], ids[4], ids[5], ids[6], ids[7], ids[8], ids[9], ids[10]);
            var empty = new SqliteConnection("DataSource=:memory:");
            await empty.OpenAsync();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(empty).Options;

            await using (var context = new AppDbContext(options))
            {
                await context.Database.MigrateAsync(await LegacySchemaSeed.PreviousMigrationAsync(context, "CleaningTaskEditing"));
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Houses" ("Id", "Name", "InviteCode", "TimeZoneId") VALUES ({houseId}, {"Casa"}, {"ABCDEFGH"}, {"Europe/Rome"});
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Rooms" ("Id", "Name", "HouseId") VALUES ({kitchenId}, {"Cucina"}, {houseId}), ({bathId}, {"Bagno"}, {houseId});
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Tenants" ("Id", "Name", "HouseId") VALUES ({tenantId}, {"Salvo"}, {houseId});
                    """);
                // Same description in two rooms: the item must be linked to the task of its own room
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "CleaningTasks" ("Id", "Description", "RoomId")
                    VALUES ({washTaskId}, {"Lavare"}, {kitchenId}), ({bathWashTaskId}, {"Lavare"}, {bathId}), ({orphanTaskId}, {"Orfana"}, NULL);
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Shifts" ("Id", "HouseId", "AssignedRoomId", "AssignedTenantId", "StartDate", "EndDate", "DueDate")
                    VALUES ({shiftId}, {houseId}, {kitchenId}, {tenantId}, {"2026-11-16"}, {"2026-11-22"}, {"2026-11-22 23:00:00"});
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "ShiftChecklistItem" ("Id", "Description", "IsCompleted", "ShiftId")
                    VALUES ({linkedItemId}, {"Lavare"}, 0, {shiftId}), ({unmatchedItemId}, {"Voce rinominata"}, 0, {shiftId}),
                           ({orphanItemId}, {"Orfana"}, 0, NULL);
                    """);

                await context.Database.MigrateAsync();
            }

            await using (var context = new AppDbContext(options))
            {
                var shift = (await new ShiftRepository(context).GetByIdAsync(houseId, shiftId))!;
                shift.Checklist.Single(i => i.Id == linkedItemId).CleaningTaskId.Should().Be(washTaskId);
                shift.Checklist.Single(i => i.Id == unmatchedItemId).CleaningTaskId.Should().BeNull();
                (await CountAsync(context, "SELECT COUNT(*) FROM \"ShiftChecklistItem\"")).Should().Be(2);
                (await CountAsync(context, "SELECT COUNT(*) FROM \"CleaningTasks\"")).Should().Be(2);
            }

            await empty.DisposeAsync();
        }

        private static Shift NewShift(House house, Room room, DateOnly start, Guid? rotationPlanId = null) =>
            new(house.Id, house.Tenants.Single(), room, start, start.AddDays(6), house.GetTimeZone(), rotationPlanId);

        private async Task<(Guid HouseId, Guid RoomId)> SeedHouseAsync(params string[] tasks)
        {
            var house = new House("Casa");
            house.AddTenant(new Tenant("Salvo"));
            var room = new Room("Cucina");
            foreach (var task in tasks)
            {
                room.AddCleaningTask(new CleaningTask(task));
            }

            house.AddRoom(room);

            await using var context = CreateContext();
            await new HouseRepository(context).AddAsync(house);
            return (house.Id, room.Id);
        }

        private static async Task<int> CountAsync(AppDbContext context, string sql)
        {
            var connection = context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
