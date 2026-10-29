using CoinquiLindo.Application.Auth;
using CoinquiLindo.Domain.Entities;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    internal static class AuthTestDoubles
    {
        public static readonly TimeSpan RefreshLifetime = TimeSpan.FromDays(30);

        public static TimeProvider Clock(DateTimeOffset now)
        {
            var clock = Substitute.For<TimeProvider>();
            clock.GetUtcNow().Returns(now);
            return clock;
        }

        // Deterministic tokens: "refresh-1", "refresh-2"... hashed as "hash(<token>)"
        public static IAuthTokenService TokenService(DateTimeOffset now)
        {
            var counter = 0;
            var service = Substitute.For<IAuthTokenService>();

            service.RefreshTokenLifetime.Returns(RefreshLifetime);
            service.CreateAccessToken(Arg.Any<User>())
                .Returns(ci => new AccessToken($"access-for-{ci.Arg<User>().Id}", now.AddMinutes(15)));
            service.GenerateRefreshToken().Returns(_ => $"refresh-{++counter}");
            service.HashRefreshToken(Arg.Any<string>()).Returns(ci => $"hash({ci.Arg<string>()})");

            return service;
        }
    }
}
