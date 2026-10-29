using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace CoinquiLindo.Infrastructure.Auth
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Auth:Jwt";

        // HS256 requires a key of at least 256 bits
        public const int MinSigningKeyBytes = 32;

        public string Issuer { get; set; } = "coinquilindo-api";
        public string Audience { get; set; } = "coinquilindo-mobile";

        // Never committed: provided via user-secrets (dev) or environment variables (prod)
        public string SigningKey { get; set; } = string.Empty;

        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

        public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
    }
}
