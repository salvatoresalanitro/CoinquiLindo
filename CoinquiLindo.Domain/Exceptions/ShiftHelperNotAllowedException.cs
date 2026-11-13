namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class ShiftHelperNotAllowedException()
        : BusinessRuleViolationException("Shift.HelperNotAllowed",
            "Only the assigned tenant of a rotation shift can choose a helper, and only among the tenants resting that week.");
}
