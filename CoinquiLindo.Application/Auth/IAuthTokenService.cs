using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Application.Auth
{
    // Port implemented in Infrastructure: keeps JWT/crypto libraries out of Application
    public interface IAuthTokenService
    {
        TimeSpan RefreshTokenLifetime { get; }

        AccessToken CreateAccessToken(User user);

        string GenerateRefreshToken();

        string HashRefreshToken(string refreshToken);
    }
}
