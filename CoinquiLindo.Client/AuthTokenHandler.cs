using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Client
{
    // Register as a singleton: the lock must serialize refreshes app-wide, because rotation invalidates the previous refresh token
    public sealed class AuthTokenHandler(ITokenStore tokenStore, TimeProvider timeProvider) : DelegatingHandler
    {
        private const string AuthPathPrefix = "/api/auth/";
        private const string RefreshPath = "/api/auth/refresh";

        // Refresh slightly before expiry to absorb clock skew and request latency
        private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

        private readonly SemaphoreSlim _refreshLock = new(1, 1);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri is null || request.RequestUri.AbsolutePath.StartsWith(AuthPathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return await base.SendAsync(request, cancellationToken);
            }

            var tokens = await GetFreshTokensAsync(request.RequestUri, cancellationToken);
            if (tokens is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
            }

            return await base.SendAsync(request, cancellationToken);
        }

        private async Task<AuthResponse?> GetFreshTokensAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            var tokens = await tokenStore.GetAsync(cancellationToken);
            if (tokens is null || !IsExpiring(tokens))
            {
                return tokens;
            }

            await _refreshLock.WaitAsync(cancellationToken);
            try
            {
                // Another request may have refreshed while this one was waiting
                tokens = await tokenStore.GetAsync(cancellationToken);
                if (tokens is null || !IsExpiring(tokens))
                {
                    return tokens;
                }

                return await RefreshAsync(tokens, requestUri, cancellationToken);
            }
            finally
            {
                _refreshLock.Release();
            }
        }

        private async Task<AuthResponse?> RefreshAsync(AuthResponse tokens, Uri requestUri, CancellationToken cancellationToken)
        {
            if (tokens.RefreshTokenExpiresAt <= timeProvider.GetUtcNow())
            {
                await tokenStore.ClearAsync(cancellationToken);
                return null;
            }

            try
            {
                using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, new Uri(requestUri, RefreshPath))
                {
                    Content = JsonContent.Create(new RefreshTokenRequest(tokens.RefreshToken))
                };

                using var response = await base.SendAsync(refreshRequest, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    // Refresh token revoked/expired server-side: the session is over
                    await tokenStore.ClearAsync(cancellationToken);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                {
                    return tokens;
                }

                var refreshed = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
                if (refreshed is null)
                {
                    return tokens;
                }

                await tokenStore.SaveAsync(refreshed, cancellationToken);
                return refreshed;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
            {
                // Transient failure: keep the session, the API call itself will surface the error
                return tokens;
            }
        }

        private bool IsExpiring(AuthResponse tokens) =>
            tokens.AccessTokenExpiresAt - timeProvider.GetUtcNow() <= RefreshMargin;

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshLock.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
