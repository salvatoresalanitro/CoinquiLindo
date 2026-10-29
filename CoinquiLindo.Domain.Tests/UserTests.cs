using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class UserTests
    {
        private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

        public static TheoryData<string?> BlankValues => new() { null, "", "   " };

        [Fact]
        public void Constructor_Should_CreateUser_When_DataIsValid()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            user.Id.Should().NotBeEmpty();
            user.GoogleSubject.Should().Be("google-sub-1");
            user.Email.Should().Be("salvo@gmail.com");
            user.DisplayName.Should().Be("Salvo");
            user.RefreshTokens.Should().BeEmpty();
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Constructor_Should_ThrowValidation_When_GoogleSubjectIsBlank(string? subject)
        {
            var action = () => new User(subject!, "salvo@gmail.com", "Salvo");

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("User.GoogleSubjectRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Constructor_Should_ThrowValidation_When_EmailIsBlank(string? email)
        {
            var action = () => new User("google-sub-1", email!, "Salvo");

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("User.EmailRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Constructor_Should_FallBackToEmail_When_DisplayNameIsBlank(string? displayName)
        {
            var user = new User("google-sub-1", "salvo@gmail.com", displayName);

            user.DisplayName.Should().Be("salvo@gmail.com");
        }

        [Fact]
        public void UpdateProfile_Should_SyncEmailAndDisplayName()
        {
            var user = new User("google-sub-1", "old@gmail.com", "Old");

            user.UpdateProfile("new@gmail.com", "Salvo");

            user.Email.Should().Be("new@gmail.com");
            user.DisplayName.Should().Be("Salvo");
            user.GoogleSubject.Should().Be("google-sub-1");
        }

        [Fact]
        public void AddRefreshToken_Should_AddActiveToken()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            var token = user.AddRefreshToken("hash-1", Now, Now.AddDays(30));

            user.RefreshTokens.Should().ContainSingle().Which.Should().Be(token);
            token.TokenHash.Should().Be("hash-1");
            token.ExpiresAt.Should().Be(Now.AddDays(30));
            token.IsActive(Now).Should().BeTrue();
        }

        [Fact]
        public void AddRefreshToken_Should_PruneInactiveTokens()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("expired", Now.AddDays(-40), Now.AddDays(-10));
            user.AddRefreshToken("revoked", Now.AddDays(-1), Now.AddDays(29));
            user.RevokeRefreshToken("revoked", Now);
            user.AddRefreshToken("active", Now, Now.AddDays(30));

            user.AddRefreshToken("new", Now, Now.AddDays(30));

            user.RefreshTokens.Select(t => t.TokenHash).Should().BeEquivalentTo("active", "new");
        }

        [Fact]
        public void RotateRefreshToken_Should_RevokeCurrentAndAddNew()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("old-hash", Now.AddDays(-1), Now.AddDays(29));

            var newToken = user.RotateRefreshToken("old-hash", "new-hash", Now, Now.AddDays(30));

            newToken.TokenHash.Should().Be("new-hash");
            newToken.IsActive(Now).Should().BeTrue();
            user.RefreshTokens.Where(t => t.IsActive(Now)).Select(t => t.TokenHash).Should().Equal("new-hash");
        }

        [Fact]
        public void RotateRefreshToken_Should_Throw_When_TokenIsReused()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("old-hash", Now.AddDays(-1), Now.AddDays(29));
            user.RotateRefreshToken("old-hash", "new-hash", Now, Now.AddDays(30));

            var action = () => user.RotateRefreshToken("old-hash", "another-hash", Now, Now.AddDays(30));

            action.Should().Throw<InvalidRefreshTokenException>()
                .Which.Code.Should().Be("Auth.InvalidRefreshToken");
        }

        [Fact]
        public void RotateRefreshToken_Should_Throw_When_TokenIsExpired()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("old-hash", Now.AddDays(-31), Now.AddSeconds(-1));

            var action = () => user.RotateRefreshToken("old-hash", "new-hash", Now, Now.AddDays(30));

            action.Should().Throw<InvalidRefreshTokenException>();
            user.RefreshTokens.Should().NotContain(t => t.TokenHash == "new-hash");
        }

        [Fact]
        public void RotateRefreshToken_Should_Throw_When_TokenIsUnknown()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            var action = () => user.RotateRefreshToken("unknown", "new-hash", Now, Now.AddDays(30));

            action.Should().Throw<InvalidRefreshTokenException>();
        }

        [Fact]
        public void RevokeRefreshToken_Should_DeactivateToken()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            var token = user.AddRefreshToken("hash-1", Now, Now.AddDays(30));

            user.RevokeRefreshToken("hash-1", Now);

            token.IsActive(Now).Should().BeFalse();
            token.RevokedAt.Should().Be(Now);
        }

        [Fact]
        public void RevokeRefreshToken_Should_BeNoOp_When_TokenIsUnknown()
        {
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");

            var action = () => user.RevokeRefreshToken("unknown", Now);

            action.Should().NotThrow();
        }
    }
}
