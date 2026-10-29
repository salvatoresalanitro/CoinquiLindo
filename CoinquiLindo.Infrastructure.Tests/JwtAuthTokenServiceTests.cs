using System.Security.Cryptography;
using System.Text;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class JwtAuthTokenServiceTests
    {
        private const string SigningKey = "test-signing-key-at-least-32-bytes-long!!";

        // Second precision: JWT "exp" is expressed in whole seconds
        private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        private static readonly JwtOptions JwtSettings = new()
        {
            Issuer = "coinquilindo-api",
            Audience = "coinquilindo-mobile",
            SigningKey = SigningKey,
            AccessTokenLifetime = TimeSpan.FromMinutes(15),
            RefreshTokenLifetime = TimeSpan.FromDays(30)
        };

        private readonly JwtAuthTokenService _service = new(Options.Create(JwtSettings), new FixedTimeProvider(Now));

        [Fact]
        public async Task CreateAccessToken_Should_ProduceSignedJwt_WithUserClaims()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            // Act
            var accessToken = _service.CreateAccessToken(user);

            // Assert
            accessToken.ExpiresAt.Should().Be(Now.AddMinutes(15));

            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, new TokenValidationParameters
            {
                ValidIssuer = "coinquilindo-api",
                ValidAudience = "coinquilindo-mobile",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey))
            });

            validation.IsValid.Should().BeTrue(validation.Exception?.Message);
            validation.Claims["sub"].Should().Be(user.Id.ToString());
            validation.Claims["email"].Should().Be("salvo@gmail.com");
            validation.Claims["name"].Should().Be("Salvo");
            validation.SecurityToken.ValidTo.Should().Be(Now.AddMinutes(15).UtcDateTime);
        }

        [Fact]
        public async Task CreateAccessToken_Should_BeRejected_WithDifferentKey()
        {
            var accessToken = _service.CreateAccessToken(new User("google-sub-1", "salvo@gmail.com", "Salvo"));

            var validation = await new JsonWebTokenHandler().ValidateTokenAsync(accessToken.Value, new TokenValidationParameters
            {
                ValidIssuer = "coinquilindo-api",
                ValidAudience = "coinquilindo-mobile",
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("another-signing-key-at-least-32-bytes!!"))
            });

            validation.IsValid.Should().BeFalse();
        }

        [Fact]
        public void GenerateRefreshToken_Should_ReturnUniqueHighEntropyUrlSafeValues()
        {
            var tokens = Enumerable.Range(0, 100).Select(_ => _service.GenerateRefreshToken()).ToList();

            tokens.Should().OnlyHaveUniqueItems();
            tokens.Should().AllSatisfy(t =>
            {
                Base64UrlEncoder.DecodeBytes(t).Length.Should().BeGreaterThanOrEqualTo(32);
                t.Should().MatchRegex("^[A-Za-z0-9_-]+$");
            });
        }

        [Fact]
        public void HashRefreshToken_Should_BeDeterministicSha256_AndNeverEqualToken()
        {
            var token = _service.GenerateRefreshToken();

            var hash = _service.HashRefreshToken(token);

            hash.Should().Be(_service.HashRefreshToken(token));
            hash.Should().NotBe(token);
            hash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))));
        }

        [Fact]
        public void RefreshTokenLifetime_Should_ComeFromOptions()
        {
            _service.RefreshTokenLifetime.Should().Be(TimeSpan.FromDays(30));
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }
    }
}
