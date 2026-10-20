namespace CoinquiLindo.Domain.Exceptions
{
    public abstract class BusinessRuleViolationException(string code, string message) : DomainException(code, message);
}
