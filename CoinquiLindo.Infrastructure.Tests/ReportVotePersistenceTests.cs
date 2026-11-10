using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class ReportVotePersistenceTests : IDisposable
    {
        private const int SqliteConstraintError = 19;
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.png";

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public ReportVotePersistenceTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        }

        [Fact]
        public async Task Repository_Should_PersistChangeAndRemoveVote()
        {
            var seed = await SeedReportAsync();

            await VoteAsync(seed, VoteType.Up);
            Guid voteId;
            await using (var context = CreateContext())
            {
                var report = (await LoadShiftAsync(context, seed)).Reports.Single();
                var vote = report.Votes.Should().ContainSingle().Subject;
                vote.VoterTenantId.Should().Be(seed.VoterId);
                vote.Type.Should().Be(VoteType.Up);
                voteId = vote.Id;
            }

            await VoteAsync(seed, VoteType.Down);
            await using (var context = CreateContext())
            {
                var vote = (await LoadShiftAsync(context, seed)).Reports.Single().Votes.Should().ContainSingle().Subject;
                vote.Id.Should().Be(voteId, "changing a vote must update the existing row");
                vote.Type.Should().Be(VoteType.Down);
                vote.UpdatedAt.Should().NotBeNull();
            }

            await using (var context = CreateContext())
            {
                var repository = new ShiftRepository(context);
                var shift = await LoadShiftAsync(context, seed);
                shift.RemoveReportVote(seed.ReportId, seed.VoterId);
                await repository.UpdateAsync(shift);
            }

            await using (var context = CreateContext())
            {
                (await LoadShiftAsync(context, seed)).Reports.Single().Votes.Should().BeEmpty();
                (await CountVotesAsync(context)).Should().Be(0);
            }
        }

        [Fact]
        public async Task GetByHouseId_Should_LoadReportVotes()
        {
            var seed = await SeedReportAsync();
            await VoteAsync(seed, VoteType.Down);

            await using var context = CreateContext();
            var shift = (await new ShiftRepository(context).GetByHouseIdAsync(seed.HouseId)).Single();

            shift.Reports.Single().Score.Should().Be(-1);
        }

        [Fact]
        public async Task UpdateAsync_Should_ThrowConflict_When_SameTenantVotesConcurrently()
        {
            var seed = await SeedReportAsync();
            await using var first = CreateContext();
            await using var second = CreateContext();
            var firstShift = await LoadShiftAsync(first, seed);
            var secondShift = await LoadShiftAsync(second, seed);
            var voter = await first.Tenants.SingleAsync(tenant => tenant.Id == seed.VoterId);

            firstShift.VoteReport(seed.ReportId, voter, VoteType.Up);
            secondShift.VoteReport(seed.ReportId, voter, VoteType.Down);
            await new ShiftRepository(first).UpdateAsync(firstShift);
            var action = () => new ShiftRepository(second).UpdateAsync(secondShift);

            (await action.Should().ThrowAsync<ReportVoteConflictException>()).Which.Code.Should().Be("ReportVote.Conflict");
            await using var assertContext = CreateContext();
            (await CountVotesAsync(assertContext)).Should().Be(1);
        }

        [Fact]
        public async Task UpdateAsync_Should_ThrowConflict_When_SameVoteIsRemovedConcurrently()
        {
            var seed = await SeedReportAsync();
            await VoteAsync(seed, VoteType.Up);
            await using var first = CreateContext();
            await using var second = CreateContext();
            var firstShift = await LoadShiftAsync(first, seed);
            var secondShift = await LoadShiftAsync(second, seed);

            firstShift.RemoveReportVote(seed.ReportId, seed.VoterId);
            secondShift.RemoveReportVote(seed.ReportId, seed.VoterId);
            await new ShiftRepository(first).UpdateAsync(firstShift);
            var action = () => new ShiftRepository(second).UpdateAsync(secondShift);

            await action.Should().ThrowAsync<ReportVoteConflictException>();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public async Task Database_Should_RejectInvalidVoteType(int type)
        {
            var seed = await SeedReportAsync();

            await using var context = CreateContext();
            var action = () => InsertVoteAsync(context, seed, type);

            (await action.Should().ThrowAsync<SqliteException>()).Which.SqliteErrorCode.Should().Be(SqliteConstraintError);
        }

        [Fact]
        public async Task Database_Should_RejectSecondVoteOfSameTenant()
        {
            var seed = await SeedReportAsync();
            await using var context = CreateContext();
            await InsertVoteAsync(context, seed, 1);

            var action = () => InsertVoteAsync(context, seed, -1);

            (await action.Should().ThrowAsync<SqliteException>()).Which.SqliteErrorCode.Should().Be(SqliteConstraintError);
        }

        [Fact]
        public async Task Database_Should_DeleteVotes_When_ReportIsDeleted()
        {
            var seed = await SeedReportAsync();
            await VoteAsync(seed, VoteType.Up);

            await using var context = CreateContext();
            await context.Database.ExecuteSqlInterpolatedAsync($"""DELETE FROM "Reports" WHERE "Id" = {seed.ReportId}""");

            (await CountVotesAsync(context)).Should().Be(0);
        }

        private async Task VoteAsync((Guid HouseId, Guid ShiftId, Guid ReportId, Guid VoterId) seed, VoteType type)
        {
            await using var context = CreateContext();
            var repository = new ShiftRepository(context);
            var shift = await LoadShiftAsync(context, seed);
            var voter = await context.Tenants.SingleAsync(tenant => tenant.Id == seed.VoterId);
            shift.VoteReport(seed.ReportId, voter, type);
            await repository.UpdateAsync(shift);
        }

        private static async Task<Shift> LoadShiftAsync(AppDbContext context, (Guid HouseId, Guid ShiftId, Guid ReportId, Guid VoterId) seed) =>
            (await new ShiftRepository(context).GetByIdAsync(seed.HouseId, seed.ShiftId))!;

        private static Task<int> InsertVoteAsync(AppDbContext context, (Guid HouseId, Guid ShiftId, Guid ReportId, Guid VoterId) seed, int type) =>
            context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "ReportVotes" ("Id", "ReportId", "VoterTenantId", "Type", "CreatedAt")
                VALUES ({Guid.NewGuid()}, {seed.ReportId}, {seed.VoterId}, {type}, {DateTime.UtcNow});
                """);

        private static Task<int> CountVotesAsync(AppDbContext context) =>
            context.Database.SqlQuery<int>($"""SELECT COUNT(*) AS "Value" FROM "ReportVotes" """).SingleAsync();

        private async Task<(Guid HouseId, Guid ShiftId, Guid ReportId, Guid VoterId)> SeedReportAsync()
        {
            var house = new House("Casa");
            var assignee = new Tenant("Assegnatario");
            var reporter = new Tenant("Segnalatore");
            var voter = new Tenant("Votante");
            var room = new Room("Bagno");
            house.AddTenant(assignee);
            house.AddTenant(reporter);
            house.AddTenant(voter);
            house.AddRoom(room);

            await using (var context = CreateContext())
            {
                await context.Database.MigrateAsync();
                await new HouseRepository(context).AddAsync(house);
            }

            await using (var context = CreateContext())
            {
                var shift = new Shift(house.Id,
                    await context.Tenants.SingleAsync(tenant => tenant.Id == assignee.Id),
                    await context.Rooms.SingleAsync(r => r.Id == room.Id),
                    DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
                var report = new Report(await context.Tenants.SingleAsync(tenant => tenant.Id == reporter.Id),
                    "Pavimento sporco", PhotoKey);
                shift.AddReport(report);
                await new ShiftRepository(context).AddAsync(shift);
                return (house.Id, shift.Id, report.Id, voter.Id);
            }
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
