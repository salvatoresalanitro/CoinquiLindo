using CoinquiLindo.Application.Auth;
using CoinquiLindo.Application.Auth.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class RefreshSessionCommandHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

        private readonly IUserRepository _mockUserRepository = Substitute.For<IUserRepository>();
        private readonly IAuthTokenService _tokenService = AuthTestDoubles.TokenService(Now);
        private readonly RefreshSessionCommandHandler _handler;

        public RefreshSessionCommandHandlerTests()
        {
            _handler = new RefreshSessionCommandHandler(_mockUserRepository, _tokenService, AuthTestDoubles.Clock(Now));
        }

        [Fact]
        public async Task Handle_Should_RotateRefreshToken_And_ReturnNewTokens()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("hash(old-token)", Now.UtcDateTime.AddDays(-1), Now.UtcDateTime.AddDays(29));
            _mockUserRepository.GetByRefreshTokenHashAsync("hash(old-token)", Arg.Any<CancellationToken>()).Returns(user);

            // Act
            var response = await _handler.Handle(new RefreshSessionCommand(RefreshToken: "old-token"), CancellationToken.None);

            // Assert
            response.AccessToken.Should().Be($"access-for-{user.Id}");
            response.RefreshToken.Should().Be("refresh-1");
            response.RefreshTokenExpiresAt.Should().Be(Now.Add(AuthTestDoubles.RefreshLifetime));
            user.RefreshTokens.Where(t => t.IsActive(Now.UtcDateTime)).Select(t => t.TokenHash)
                .Should().Equal("hash(refresh-1)");

            await _mockUserRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowInvalidRefreshToken_WhenTokenIsUnknown()
        {
            // Arrange
            _mockUserRepository.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(new RefreshSessionCommand(RefreshToken: "unknown"), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<InvalidRefreshTokenException>()
                .Where(e => e.Code == "Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task Handle_Should_ThrowInvalidRefreshToken_And_NotSave_WhenTokenIsExpired()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("hash(old-token)", Now.UtcDateTime.AddDays(-31), Now.UtcDateTime.AddMinutes(-1));
            _mockUserRepository.GetByRefreshTokenHashAsync("hash(old-token)", Arg.Any<CancellationToken>()).Returns(user);

            // Act
            Func<Task> action = async () => await _handler.Handle(new RefreshSessionCommand(RefreshToken: "old-token"), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<InvalidRefreshTokenException>();
            await _mockUserRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task Handle_Should_ThrowInvalidRefreshToken_WhenTokenIsBlank(string? token)
        {
            // Act
            Func<Task> action = async () => await _handler.Handle(new RefreshSessionCommand(RefreshToken: token!), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<InvalidRefreshTokenException>();
            await _mockUserRepository.DidNotReceive().GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }
    }
}
