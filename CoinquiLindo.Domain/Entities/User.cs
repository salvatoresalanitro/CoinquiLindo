using System.Diagnostics.CodeAnalysis;
using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    // Account identified by Google "sub" (stable), not by email (can change)
    public class User
    {
        public Guid Id { get; private set; }
        public string GoogleSubject { get; private set; }
        public string Email { get; private set; }
        public string DisplayName { get; private set; }

        private readonly List<RefreshToken> _refreshTokens = new();

        public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

        public User(string googleSubject, string email, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(googleSubject))
            {
                throw new DomainValidationException("User.GoogleSubjectRequired", "Google subject cannot be empty.");
            }

            Id = Guid.NewGuid();
            GoogleSubject = googleSubject;
            SetProfile(email, displayName);
        }

        public void UpdateProfile(string email, string? displayName) => SetProfile(email, displayName);

        public RefreshToken AddRefreshToken(string tokenHash, DateTime createdAt, DateTime expiresAt)
        {
            // Keeps the table bounded: expired/revoked tokens are useless once a new one is issued
            _refreshTokens.RemoveAll(t => !t.IsActive(createdAt));

            var token = new RefreshToken(tokenHash, createdAt, expiresAt);
            _refreshTokens.Add(token);

            return token;
        }

        public RefreshToken RotateRefreshToken(string currentHash, string newHash, DateTime now, DateTime newExpiresAt)
        {
            var current = _refreshTokens.FirstOrDefault(t => t.TokenHash == currentHash);
            if (current is null || !current.IsActive(now))
            {
                throw new InvalidRefreshTokenException();
            }

            current.Revoke(now);

            return AddRefreshToken(newHash, now, newExpiresAt);
        }

        public void RevokeRefreshToken(string tokenHash, DateTime now)
        {
            _refreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash)?.Revoke(now);
        }

        [MemberNotNull(nameof(Email), nameof(DisplayName))]
        private void SetProfile(string email, string? displayName)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new DomainValidationException("User.EmailRequired", "Email cannot be empty.");
            }

            Email = email.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? Email : displayName.Trim();
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private User() { }
#pragma warning restore CS8618
    }
}
