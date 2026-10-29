using System.Security.Cryptography;
using System.Text;
using CoinquiLindo.Application.Auth;
using CoinquiLindo.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoinquiLindo.Infrastructure.Auth
{
    public sealed class JwtAuthTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAuthTokenService
    {
        private const int RefreshTokenBytes = 32;

        private readonly JwtOptions _options = options.Value;
        private readonly JsonWebTokenHandler _handler = new();

        public TimeSpan RefreshTokenLifetime => _options.RefreshTokenLifetime;

        public AccessToken CreateAccessToken(User user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var now = timeProvider.GetUtcNow();
            var expiresAt = now + _options.AccessTokenLifetime;

            var token = _handler.CreateToken(new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                Claims = new Dictionary<string, object>
                {
                    [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
                    [JwtRegisteredClaimNames.Email] = user.Email,
                    [JwtRegisteredClaimNames.Name] = user.DisplayName,
                    [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString()
                },
                SigningCredentials = new SigningCredentials(_options.CreateSigningKey(), SecurityAlgorithms.HmacSha256)
            });

            return new AccessToken(token, expiresAt);
        }

        public string GenerateRefreshToken() =>
            Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

        // High-entropy random token: a fast unsalted hash is enough (no brute-force risk as with passwords)
        public string HashRefreshToken(string refreshToken) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
    }
}
