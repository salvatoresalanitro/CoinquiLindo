using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Reports.Commands;
using CoinquiLindo.Application.Reports.Queries;
using CoinquiLindo.Application.Shifts.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class ReportVoteHandlerTests
    {
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.jpg";

        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();

        [Theory]
        [InlineData(ReportVoteValue.Up, 1, 0, 1)]
        [InlineData(ReportVoteValue.Down, 0, 1, -1)]
        public async Task CastVote_Should_SaveVote_AndReturnUpdatedReport(ReportVoteValue vote, int ups, int downs, int score)
        {
            var scenario = Arrange();

            var response = await CastHandler().Handle(
                new CastReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, vote, scenario.Voter.Id),
                CancellationToken.None);

            response.Id.Should().Be(scenario.Report.Id);
            response.UpvoteCount.Should().Be(ups);
            response.DownvoteCount.Should().Be(downs);
            response.Score.Should().Be(score);
            response.MyVote.Should().Be(vote);
            response.CanVote.Should().BeTrue();
            scenario.Report.VoteOf(scenario.VoterTenant.Id).Should().Be((VoteType)(int)vote);
            await _shiftRepository.Received(1).UpdateAsync(scenario.Shift, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CastVote_Should_RejectNonMember_BeforeLoadingShift()
        {
            var scenario = Arrange();

            var action = () => CastHandler().Handle(
                new CastReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, ReportVoteValue.Up, Guid.NewGuid()),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await _shiftRepository.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CastVote_Should_RejectReporter_WithoutSaving()
        {
            var scenario = Arrange();

            var action = () => CastHandler().Handle(
                new CastReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, ReportVoteValue.Up, scenario.Reporter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<ReportVoteNotAllowedException>();
            await _shiftRepository.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CastVote_Should_RejectUndefinedVote_WithoutSaving()
        {
            var scenario = Arrange();

            var action = () => CastHandler().Handle(
                new CastReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, (ReportVoteValue)7, scenario.Voter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<DomainValidationException>().Where(e => e.Code == "ReportVote.InvalidType");
            await _shiftRepository.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CastVote_Should_ThrowReportNotFound_WithoutSaving()
        {
            var scenario = Arrange();

            var action = () => CastHandler().Handle(
                new CastReportVoteCommand(scenario.House.Id, scenario.Shift.Id, Guid.NewGuid(), ReportVoteValue.Up, scenario.Voter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "Report.NotFound");
            await _shiftRepository.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RemoveVote_Should_SaveAndReturnReportWithoutMyVote()
        {
            var scenario = Arrange();
            scenario.Shift.VoteReport(scenario.Report.Id, scenario.VoterTenant, VoteType.Up);

            var response = await RemoveHandler().Handle(
                new RemoveReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, scenario.Voter.Id),
                CancellationToken.None);

            response.MyVote.Should().BeNull();
            response.Score.Should().Be(0);
            scenario.Report.Votes.Should().BeEmpty();
            await _shiftRepository.Received(1).UpdateAsync(scenario.Shift, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RemoveVote_Should_RejectNonMember()
        {
            var scenario = Arrange();

            var action = () => RemoveHandler().Handle(
                new RemoveReportVoteCommand(scenario.House.Id, scenario.Shift.Id, scenario.Report.Id, Guid.NewGuid()),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetShiftReports_Should_ComputeVoteStateForTheRequester()
        {
            var scenario = Arrange();
            scenario.Shift.VoteReport(scenario.Report.Id, scenario.VoterTenant, VoteType.Down);
            var handler = new GetShiftReportsQueryHandler(_houseRepository, _shiftRepository);

            var asVoter = (await handler.Handle(
                new GetShiftReportsQuery(scenario.House.Id, scenario.Shift.Id, scenario.Voter.Id), CancellationToken.None)).Single();
            var asReporter = (await handler.Handle(
                new GetShiftReportsQuery(scenario.House.Id, scenario.Shift.Id, scenario.Reporter.Id), CancellationToken.None)).Single();
            var asAssignee = (await handler.Handle(
                new GetShiftReportsQuery(scenario.House.Id, scenario.Shift.Id, scenario.Assignee.Id), CancellationToken.None)).Single();

            asVoter.Should().BeEquivalentTo(new { UpvoteCount = 0, DownvoteCount = 1, Score = -1, MyVote = (ReportVoteValue?)ReportVoteValue.Down, CanVote = true });
            asReporter.Should().BeEquivalentTo(new { Score = -1, MyVote = (ReportVoteValue?)null, CanVote = false });
            asAssignee.CanVote.Should().BeFalse();
        }

        [Fact]
        public async Task GetHouseShifts_Should_ExposeCanReportAndReportVotes_ForTheRequester()
        {
            var scenario = Arrange();
            scenario.Shift.VoteReport(scenario.Report.Id, scenario.VoterTenant, VoteType.Up);
            _shiftRepository.GetByHouseIdAsync(scenario.House.Id, Arg.Any<CancellationToken>()).Returns([scenario.Shift]);
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository, Substitute.For<IRotationPlanRepository>());

            var asVoter = (await handler.Handle(new GetHouseShiftsQuery(scenario.House.Id, scenario.Voter.Id), CancellationToken.None)).Single();
            var asAssignee = (await handler.Handle(new GetHouseShiftsQuery(scenario.House.Id, scenario.Assignee.Id), CancellationToken.None)).Single();

            asVoter.CanReport.Should().BeTrue();
            asVoter.Reports.Single().Should().BeEquivalentTo(new { UpvoteCount = 1, Score = 1, MyVote = (ReportVoteValue?)ReportVoteValue.Up, CanVote = true });
            asAssignee.CanReport.Should().BeFalse();
            asAssignee.Reports.Single().Should().BeEquivalentTo(new { Score = 1, MyVote = (ReportVoteValue?)null, CanVote = false });
        }

        private CastReportVoteCommandHandler CastHandler() => new(_houseRepository, _shiftRepository,
            new NotificationPublisher(Substitute.For<INotificationRepository>(), TimeProvider.System, Substitute.For<IPushDispatcher>()));

        private RemoveReportVoteCommandHandler RemoveHandler() => new(_houseRepository, _shiftRepository);

        private Scenario Arrange()
        {
            var house = new House("Casa");
            var assignee = TestData.NewUser("Lucia");
            var reporter = TestData.NewUser("Salvo");
            var voter = TestData.NewUser("Marco");
            var assigneeTenant = house.AddMember(assignee);
            var reporterTenant = house.AddMember(reporter);
            var voterTenant = house.AddMember(voter);
            var room = new Room("Bagno");
            house.AddRoom(room);
            var shift = new Shift(house.Id, assigneeTenant, room, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            var report = new Report(reporterTenant, "Pavimento sporco", PhotoKey);
            shift.AddReport(report);
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByIdAsync(house.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            return new Scenario(house, assignee, reporter, voter, voterTenant, shift, report);
        }

        private sealed record Scenario(
            House House, User Assignee, User Reporter, User Voter, Tenant VoterTenant, Shift Shift, Report Report);
    }
}
