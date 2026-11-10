namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class ReportVoteNotAllowedException()
        : BusinessRuleViolationException("ReportVote.NotAllowed", "The reporter and the assigned tenant cannot vote on a report.");
}
