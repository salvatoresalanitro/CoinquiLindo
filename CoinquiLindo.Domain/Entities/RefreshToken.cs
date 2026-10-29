namespace CoinquiLindo.Domain.Entities
{
    // Only the SHA-256 hash is persisted: a leaked database cannot be used to refresh sessions
    public class RefreshToken
    {
        public Guid Id { get; private set; }
        public string TokenHash { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? RevokedAt { get; private set; }

        internal RefreshToken(string tokenHash, DateTime createdAt, DateTime expiresAt)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash, nameof(tokenHash));
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expiresAt, createdAt, nameof(expiresAt));

            Id = Guid.NewGuid();
            TokenHash = tokenHash;
            CreatedAt = createdAt;
            ExpiresAt = expiresAt;
        }

        public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

        internal void Revoke(DateTime now) => RevokedAt ??= now;

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private RefreshToken() { }
#pragma warning restore CS8618
    }
}
