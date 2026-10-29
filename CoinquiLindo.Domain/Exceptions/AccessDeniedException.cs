namespace CoinquiLindo.Domain.Exceptions
{
    // The caller is authenticated but not allowed to act on the resource
    public abstract class AccessDeniedException(string code, string message) : DomainException(code, message);
}
