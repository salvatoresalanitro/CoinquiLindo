using System.Text.Json;
using CoinquiLindo.Client;
using CoinquiLindo.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Mobile.Services
{
    // Singleton: keeps an in-memory copy because SecureStorage reads are slow (Keystore/Keychain round-trips)
    public sealed class SecureTokenStore(ILogger<SecureTokenStore> logger) : ITokenStore
    {
        private const string StorageKey = "coinquilindo.auth";

        private AuthResponse? _cached;
        private bool _loaded;

        // Raised when the session ends (logout or refresh token rejected): the UI redirects to the login page
        public event Action? SessionEnded;

        public async Task<AuthResponse?> GetAsync(CancellationToken cancellationToken = default)
        {
            if (_loaded)
            {
                return _cached;
            }

            try
            {
                var json = await SecureStorage.Default.GetAsync(StorageKey);
                _cached = json is null ? null : JsonSerializer.Deserialize<AuthResponse>(json, JsonSerializerOptions.Web);
            }
            catch (Exception ex)
            {
                // Corrupted entry or keystore reset (e.g. app restored from backup): start a new session
                logger.LogWarning(ex, "Unable to read stored session");
                SecureStorage.Default.Remove(StorageKey);
                _cached = null;
            }

            _loaded = true;
            return _cached;
        }

        public async Task SaveAsync(AuthResponse tokens, CancellationToken cancellationToken = default)
        {
            await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(tokens, JsonSerializerOptions.Web));

            _cached = tokens;
            _loaded = true;
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            var hadSession = _cached is not null;

            SecureStorage.Default.Remove(StorageKey);
            _cached = null;
            _loaded = true;

            if (hadSession)
            {
                SessionEnded?.Invoke();
            }

            return Task.CompletedTask;
        }
    }
}
