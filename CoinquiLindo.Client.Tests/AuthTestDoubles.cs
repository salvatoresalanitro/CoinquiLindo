using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Client.Tests
{
    internal sealed class InMemoryTokenStore(AuthResponse? initial = null) : ITokenStore
    {
        public AuthResponse? Current { get; private set; } = initial;
        public int SaveCount { get; private set; }

        public Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default) => Task.FromResult(Current);

        public Task SaveAsync(AuthResponse tokens, CancellationToken cancellationToken = default)
        {
            Current = tokens;
            SaveCount++;
            return Task.CompletedTask;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Current = null;
            return Task.CompletedTask;
        }
    }

    internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
