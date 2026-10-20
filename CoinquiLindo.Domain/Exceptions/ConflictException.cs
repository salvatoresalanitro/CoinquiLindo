namespace CoinquiLindo.Domain.Exceptions
{
    public abstract class ConflictException(string code, string message) : DomainException(code, message);
}
