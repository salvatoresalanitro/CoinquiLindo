namespace CoinquiLindo.Application.Auth
{
    public record AccessToken(string Value, DateTimeOffset ExpiresAt);
}
