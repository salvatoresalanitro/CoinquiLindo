using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class ReportPersistenceTests : IDisposable
    {
        private const int SqliteConstraintError = 19;
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.png";

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public ReportPersistenceTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Database_Should_RejectReportWithoutPhoto(string? photoKey)
        {
            await MigrateAsync();
            var seed = await SeedShiftAsync();

            await using var context = CreateContext();
            var action = () => context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Reports" ("Id", "ReporterId", "Description", "PhotoKey", "CreatedAt", "ShiftId")
                VALUES ({Guid.NewGuid()}, {seed.ReporterId}, {"Pavimento ancora sporco"}, {photoKey}, {DateTime.UtcNow}, {seed.ShiftId});
                """);

            (await action.Should().ThrowAsync<SqliteException>())
                .Which.SqliteErrorCode.Should().Be(SqliteConstraintError);
        }

        [Fact]
        public async Task Repository_Should_PersistReportAddedToShift_AndLoadItWithReporter()
        {
            await MigrateAsync();
            var seed = await SeedShiftAsync();
            Guid reportId;

            await using (var context = CreateContext())
            {
                var repository = new ShiftRepository(context);
                var shift = await repository.GetByIdAsync(seed.HouseId, seed.ShiftId);
                shift!.AssignedTenant.Id.Should().Be(seed.AssigneeId);
                var reporter = await context.Tenants.SingleAsync(tenant => tenant.Id == seed.ReporterId);
                var report = new Report(reporter, "Pavimento ancora sporco", PhotoKey);
                shift.AddReport(report);
                await repository.UpdateAsync(shift);
                reportId = report.Id;
            }

            await using (var context = CreateContext())
            {
                var shift = await new ShiftRepository(context).GetByIdAsync(seed.HouseId, seed.ShiftId);
                var report = shift!.Reports.Should().ContainSingle().Subject;
                report.Id.Should().Be(reportId);
                report.PhotoKey.Should().Be(PhotoKey);
                report.Reporter.Name.Should().Be("Segnalatore");
            }

            await using (var context = CreateContext())
            {
                var shift = (await new ShiftRepository(context).GetByHouseIdAsync(seed.HouseId)).Single();
                shift.Reports.Should().ContainSingle(report =>
                    report.Id == reportId && report.Reporter.Name == "Segnalatore");
            }
        }

        [Fact]
        public async Task RequireReportPhotoMigration_Should_Fail_When_ExistingReportHasNoPhoto()
        {
            var reportId = Guid.NewGuid();
            await using (var context = CreateContext())
            {
                var previousMigration = context.Database.GetMigrations()
                    .Single(migration => migration.EndsWith("_AddShiftHouseId", StringComparison.Ordinal));
                await context.Database.MigrateAsync(previousMigration);
            }

            var seed = await SeedShiftAsync();
            await using (var context = CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Reports" ("Id", "ReporterId", "Description", "PhotoUrl", "CreatedAt", "ShiftId")
                    VALUES ({reportId}, {seed.ReporterId}, {"Segnalazione legacy"}, NULL, {DateTime.UtcNow}, {seed.ShiftId});
                    """);
            }

            await using (var context = CreateContext())
            {
                var action = () => context.Database.MigrateAsync();

                await action.Should().ThrowAsync<SqliteException>();
            }

            await using var assertContext = CreateContext();
            var legacyReports = await assertContext.Database
                .SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM "Reports" WHERE "Id" = {reportId}""")
                .SingleAsync();
            legacyReports.Should().Be(1);
        }

        private async Task MigrateAsync()
        {
            await using var context = CreateContext();
            await context.Database.MigrateAsync();
        }

        private async Task<(Guid HouseId, Guid ShiftId, Guid AssigneeId, Guid ReporterId)> SeedShiftAsync()
        {
            var house = new House("Casa");
            var assignee = new Tenant("Assegnatario");
            var reporter = new Tenant("Segnalatore");
            var room = new Room("Bagno");
            house.AddTenant(assignee);
            house.AddTenant(reporter);
            house.AddRoom(room);
            var shiftId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            await using var context = CreateContext();
            await new HouseRepository(context).AddAsync(house);
            // Raw SQL keeps the seed valid against older schemas used by migration tests
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Shifts" ("Id", "HouseId", "AssignedRoomId", "AssignedTenantId", "StartDate", "EndDate")
                VALUES ({shiftId}, {house.Id}, {room.Id}, {assignee.Id}, {now}, {now.AddDays(7)});
                """);

            return (house.Id, shiftId, assignee.Id, reporter.Id);
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
