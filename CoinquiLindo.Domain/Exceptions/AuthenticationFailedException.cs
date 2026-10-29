namespace CoinquiLindo.Domain.Exceptions
{
    // The caller's credentials (e.g. refresh token) are missing, expired or revoked
    public abstract class AuthenticationFailedException(string code, string message) : DomainException(code, message);
}
