using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class ReportVoteTests
    {
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.jpg";

        private readonly Tenant _assignee = new("Lucia");
        private readonly Tenant _reporter = new("Salvo");
        private readonly Tenant _voter = new("Marco");
        private readonly Shift _shift;
        private readonly Report _report;

        public ReportVoteTests()
        {
            _shift = new Shift(Guid.NewGuid(), _assignee, new Room("Bagno"), new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            _report = new Report(_reporter, "Pavimento sporco", PhotoKey);
            _shift.AddReport(_report);
        }

        [Fact]
        public void NewReport_Should_HaveNoVotes()
        {
            _report.Votes.Should().BeEmpty();
            _report.UpvoteCount.Should().Be(0);
            _report.DownvoteCount.Should().Be(0);
            _report.Score.Should().Be(0);
            _report.VoteOf(_voter.Id).Should().BeNull();
        }

        [Theory]
        [InlineData(VoteType.Up, 1, 0, 1)]
        [InlineData(VoteType.Down, 0, 1, -1)]
        public void VoteReport_Should_RecordVoteOfTheTenant(VoteType type, int ups, int downs, int score)
        {
            _shift.VoteReport(_report.Id, _voter, type);

            var vote = _report.Votes.Should().ContainSingle().Subject;
            vote.VoterTenantId.Should().Be(_voter.Id);
            vote.Type.Should().Be(type);
            vote.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            _report.UpvoteCount.Should().Be(ups);
            _report.DownvoteCount.Should().Be(downs);
            _report.Score.Should().Be(score);
            _report.VoteOf(_voter.Id).Should().Be(type);
        }

        [Fact]
        public void VoteReport_Should_ChangeExistingVote_InsteadOfAddingASecondOne()
        {
            _shift.VoteReport(_report.Id, _voter, VoteType.Up);
            var original = _report.Votes.Single();

            _shift.VoteReport(_report.Id, _voter, VoteType.Down);

            var vote = _report.Votes.Should().ContainSingle().Subject;
            vote.Should().BeSameAs(original);
            vote.Type.Should().Be(VoteType.Down);
            vote.UpdatedAt.Should().NotBeNull();
            _report.Score.Should().Be(-1);
        }

        [Fact]
        public void VoteReport_Should_BeIdempotent_When_SameVoteIsCastTwice()
        {
            _shift.VoteReport(_report.Id, _voter, VoteType.Up);

            _shift.VoteReport(_report.Id, _voter, VoteType.Up);

            _report.Votes.Should().ContainSingle().Which.Type.Should().Be(VoteType.Up);
            _report.Score.Should().Be(1);
        }

        [Fact]
        public void VoteReport_Should_AggregateVotesOfDifferentTenants()
        {
            _shift.VoteReport(_report.Id, _voter, VoteType.Up);
            _shift.VoteReport(_report.Id, new Tenant("Anna"), VoteType.Up);
            _shift.VoteReport(_report.Id, new Tenant("Paolo"), VoteType.Down);

            _report.UpvoteCount.Should().Be(2);
            _report.DownvoteCount.Should().Be(1);
            _report.Score.Should().Be(1);
        }

        [Fact]
        public void VoteReport_Should_RejectReporter()
        {
            var action = () => _shift.VoteReport(_report.Id, _reporter, VoteType.Up);

            action.Should().Throw<ReportVoteNotAllowedException>().Which.Code.Should().Be("ReportVote.NotAllowed");
            _report.Votes.Should().BeEmpty();
        }

        [Fact]
        public void VoteReport_Should_RejectAssignedTenant()
        {
            var action = () => _shift.VoteReport(_report.Id, _assignee, VoteType.Down);

            action.Should().Throw<ReportVoteNotAllowedException>().Which.Code.Should().Be("ReportVote.NotAllowed");
            _report.Votes.Should().BeEmpty();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(-2)]
        public void VoteReport_Should_RejectUndefinedVoteType(int value)
        {
            var action = () => _shift.VoteReport(_report.Id, _voter, (VoteType)value);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("ReportVote.InvalidType");
            _report.Votes.Should().BeEmpty();
        }

        [Fact]
        public void VoteReport_Should_ThrowReportNotFound_When_ReportIsNotInShift()
        {
            var action = () => _shift.VoteReport(Guid.NewGuid(), _voter, VoteType.Up);

            action.Should().Throw<NotFoundException>().Which.Code.Should().Be("Report.NotFound");
        }

        [Fact]
        public void RemoveReportVote_Should_RemoveVoteOfTheTenantOnly()
        {
            var other = new Tenant("Anna");
            _shift.VoteReport(_report.Id, _voter, VoteType.Up);
            _shift.VoteReport(_report.Id, other, VoteType.Down);

            _shift.RemoveReportVote(_report.Id, _voter.Id);

            _report.Votes.Should().ContainSingle().Which.VoterTenantId.Should().Be(other.Id);
            _report.VoteOf(_voter.Id).Should().BeNull();
        }

        [Fact]
        public void RemoveReportVote_Should_BeNoOp_When_TenantHasNotVoted()
        {
            var action = () => _shift.RemoveReportVote(_report.Id, _voter.Id);

            action.Should().NotThrow();
            _report.Votes.Should().BeEmpty();
        }

        [Fact]
        public void RemoveReportVote_Should_ThrowReportNotFound_When_ReportIsNotInShift()
        {
            var action = () => _shift.RemoveReportVote(Guid.NewGuid(), _voter.Id);

            action.Should().Throw<NotFoundException>().Which.Code.Should().Be("Report.NotFound");
        }

        [Fact]
        public void CanVoteOnReport_Should_ExcludeReporterAndAssignee()
        {
            _shift.CanVoteOnReport(_report, _voter.Id).Should().BeTrue();
            _shift.CanVoteOnReport(_report, _reporter.Id).Should().BeFalse();
            _shift.CanVoteOnReport(_report, _assignee.Id).Should().BeFalse();
        }

        [Fact]
        public void CanReport_Should_ExcludeAssignee()
        {
            _shift.CanReport(_reporter.Id).Should().BeTrue();
            _shift.CanReport(_assignee.Id).Should().BeFalse();
        }
    }
}
