using CoinquiLindo.Application.Auth;
using CoinquiLindo.Application.Auth.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class SignOutCommandHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

        private readonly IUserRepository _mockUserRepository = Substitute.For<IUserRepository>();
        private readonly IAuthTokenService _tokenService = AuthTestDoubles.TokenService(Now);
        private readonly SignOutCommandHandler _handler;

        public SignOutCommandHandlerTests()
        {
            _handler = new SignOutCommandHandler(_mockUserRepository, _tokenService, AuthTestDoubles.Clock(Now));
        }

        [Fact]
        public async Task Handle_Should_RevokeRefreshToken_And_Save()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            var token = user.AddRefreshToken("hash(my-token)", Now.UtcDateTime, Now.UtcDateTime.AddDays(30));
            _mockUserRepository.GetByRefreshTokenHashAsync("hash(my-token)", Arg.Any<CancellationToken>()).Returns(user);

            // Act
            await _handler.Handle(new SignOutCommand(RefreshToken: "my-token"), CancellationToken.None);

            // Assert
            token.IsActive(Now.UtcDateTime).Should().BeFalse();
            await _mockUserRepository.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_BeIdempotent_WhenTokenIsUnknown()
        {
            // Arrange
            _mockUserRepository.GetByRefreshTokenHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(new SignOutCommand(RefreshToken: "unknown"), CancellationToken.None);

            // Assert
            await action.Should().NotThrowAsync();
            await _mockUserRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }
    }
}
