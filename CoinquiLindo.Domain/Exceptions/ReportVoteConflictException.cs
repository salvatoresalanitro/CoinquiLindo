namespace CoinquiLindo.Domain.Exceptions
{
    // Raised by persistence when the same tenant's vote was changed concurrently
    public sealed class ReportVoteConflictException()
        : ConflictException("ReportVote.Conflict", "The vote was modified concurrently. Reload and retry.");
}
