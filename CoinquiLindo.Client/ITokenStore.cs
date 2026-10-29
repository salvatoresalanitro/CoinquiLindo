using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Client
{
    // Implemented per platform (e.g. SecureStorage on mobile): tokens must never be stored in plain preferences
    public interface ITokenStore
    {
        Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default);

        Task SaveAsync(AuthResponse tokens, CancellationToken cancellationToken = default);

        Task ClearAsync(CancellationToken cancellationToken = default);
    }
}
