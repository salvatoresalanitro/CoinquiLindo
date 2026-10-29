using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Auth
{
    internal static class AuthSessionIssuer
    {
        // Adds a fresh refresh token to the user (caller persists) and builds the token pair
        public static AuthResponse Issue(User user, IAuthTokenService tokenService, DateTimeOffset now, string? rotatedTokenHash = null)
        {
            var refreshToken = tokenService.GenerateRefreshToken();
            var refreshTokenHash = tokenService.HashRefreshToken(refreshToken);
            var refreshExpiresAt = now + tokenService.RefreshTokenLifetime;

            if (rotatedTokenHash is null)
            {
                user.AddRefreshToken(refreshTokenHash, now.UtcDateTime, refreshExpiresAt.UtcDateTime);
            }
            else
            {
                user.RotateRefreshToken(rotatedTokenHash, refreshTokenHash, now.UtcDateTime, refreshExpiresAt.UtcDateTime);
            }

            var accessToken = tokenService.CreateAccessToken(user);

            return new AuthResponse(accessToken.Value, accessToken.ExpiresAt, refreshToken, refreshExpiresAt);
        }
    }
}
