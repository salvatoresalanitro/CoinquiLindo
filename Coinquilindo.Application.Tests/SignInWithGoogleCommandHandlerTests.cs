using CoinquiLindo.Application.Auth;
using CoinquiLindo.Application.Auth.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class SignInWithGoogleCommandHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

        private readonly IUserRepository _mockUserRepository = Substitute.For<IUserRepository>();
        private readonly IAuthTokenService _tokenService = AuthTestDoubles.TokenService(Now);
        private readonly SignInWithGoogleCommandHandler _handler;

        public SignInWithGoogleCommandHandlerTests()
        {
            _handler = new SignInWithGoogleCommandHandler(_mockUserRepository, _tokenService, AuthTestDoubles.Clock(Now));
        }

        [Fact]
        public async Task Handle_Should_CreateUser_And_ReturnTokens_WhenFirstLogin()
        {
            // Arrange
            _mockUserRepository.GetByGoogleSubjectAsync("google-sub-1", Arg.Any<CancellationToken>()).ReturnsNull();
            var command = new SignInWithGoogleCommand(GoogleSubject: "google-sub-1", Email: "salvo@gmail.com", DisplayName: "Salvo");

            // Act
            var response = await _handler.Handle(command, CancellationToken.None);

            // Assert
            response.AccessToken.Should().StartWith("access-for-");
            response.AccessTokenExpiresAt.Should().Be(Now.AddMinutes(15));
            response.RefreshToken.Should().Be("refresh-1");
            response.RefreshTokenExpiresAt.Should().Be(Now.Add(AuthTestDoubles.RefreshLifetime));

            await _mockUserRepository.Received(1).AddAsync(
                Arg.Is<User>(u => u.GoogleSubject == "google-sub-1"
                    && u.Email == "salvo@gmail.com"
                    && u.RefreshTokens.Single().TokenHash == "hash(refresh-1)"
                    && u.RefreshTokens.Single().ExpiresAt == Now.Add(AuthTestDoubles.RefreshLifetime).UtcDateTime),
                Arg.Any<CancellationToken>());
            await _mockUserRepository.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_UpdateProfile_And_KeepSameUser_WhenUserAlreadyExists()
        {
            // Arrange
            var existing = new User("google-sub-1", "old@gmail.com", "Old");
            _mockUserRepository.GetByGoogleSubjectAsync("google-sub-1", Arg.Any<CancellationToken>()).Returns(existing);
            var command = new SignInWithGoogleCommand(GoogleSubject: "google-sub-1", Email: "salvo@gmail.com", DisplayName: "Salvo");

            // Act
            var response = await _handler.Handle(command, CancellationToken.None);

            // Assert
            response.AccessToken.Should().Be($"access-for-{existing.Id}");
            existing.Email.Should().Be("salvo@gmail.com");
            existing.DisplayName.Should().Be("Salvo");
            existing.RefreshTokens.Should().ContainSingle(t => t.TokenHash == "hash(refresh-1)");

            await _mockUserRepository.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
            await _mockUserRepository.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }
    }
}
