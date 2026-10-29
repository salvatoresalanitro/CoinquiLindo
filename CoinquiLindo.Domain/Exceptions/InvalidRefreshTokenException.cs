namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class InvalidRefreshTokenException()
        : AuthenticationFailedException("Auth.InvalidRefreshToken", "The refresh token is invalid, expired or revoked.");
}
