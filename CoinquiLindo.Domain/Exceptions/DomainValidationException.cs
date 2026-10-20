namespace CoinquiLindo.Domain.Exceptions
{
    public class DomainValidationException(string code, string message) : DomainException(code, message);
}
