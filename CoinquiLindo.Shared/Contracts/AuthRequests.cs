namespace CoinquiLindo.Shared.Contracts
{
    public record RefreshTokenRequest(string RefreshToken);

    // Development-only login (POST /api/auth/dev-login): never exposed outside the Development environment
    public record DevLoginRequest(string Email, string DisplayName);
}
