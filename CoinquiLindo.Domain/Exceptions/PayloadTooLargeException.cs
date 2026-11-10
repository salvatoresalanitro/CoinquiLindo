namespace CoinquiLindo.Domain.Exceptions
{
    public abstract class PayloadTooLargeException(string code, string message) : DomainException(code, message);
}
