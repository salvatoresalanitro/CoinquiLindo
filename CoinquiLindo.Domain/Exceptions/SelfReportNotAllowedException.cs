namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class SelfReportNotAllowedException()
        : BusinessRuleViolationException("Shift.SelfReportNotAllowed", "A tenant cannot report their own shift.");
}
