using System.Globalization;
using CoinquiLindo.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Client
{
    public sealed class AuthClient(CoinquiLindoApiClient apiClient, ITokenStore tokenStore, ILogger<AuthClient> logger)
    {
        public const string SignInFailedCode = "Auth.SignInFailed";
        public const string GoogleSignInPath = "/api/auth/google";

        // Keys written by the API in the coinquilindo://auth#... redirect fragment
        private const string AccessTokenKey = "access_token";
        private const string AccessTokenExpiresAtKey = "access_token_expires_at";
        private const string RefreshTokenKey = "refresh_token";
        private const string RefreshTokenExpiresAtKey = "refresh_token_expires_at";
        private const string ErrorKey = "error";

        public async Task<bool> IsSignedInAsync(CancellationToken cancellationToken = default) =>
            await tokenStore.GetAsync(cancellationToken) is not null;

        public async Task<ApiResult> CompleteGoogleSignInAsync(IReadOnlyDictionary<string, string> callbackProperties, CancellationToken cancellationToken = default)
        {
            if (callbackProperties.TryGetValue(ErrorKey, out var error))
            {
                logger.LogWarning("Google sign-in returned error {Error}", error);
                return SignInFailed();
            }

            if (!callbackProperties.TryGetValue(AccessTokenKey, out var accessToken)
                || !callbackProperties.TryGetValue(RefreshTokenKey, out var refreshToken)
                || !TryReadUnixTime(callbackProperties, AccessTokenExpiresAtKey, out var accessExpiresAt)
                || !TryReadUnixTime(callbackProperties, RefreshTokenExpiresAtKey, out var refreshExpiresAt)
                || string.IsNullOrWhiteSpace(accessToken)
                || string.IsNullOrWhiteSpace(refreshToken))
            {
                logger.LogWarning("Google sign-in callback is incomplete");
                return SignInFailed();
            }

            await tokenStore.SaveAsync(new AuthResponse(accessToken, accessExpiresAt, refreshToken, refreshExpiresAt), cancellationToken);

            return ApiResult.Success();
        }

        public async Task<ApiResult> DevLoginAsync(string email, string displayName, CancellationToken cancellationToken = default)
        {
            var result = await apiClient.DevLoginAsync(email, displayName, cancellationToken);
            if (!result.IsSuccess)
            {
                return ApiResult.Failure(result.Error);
            }

            await tokenStore.SaveAsync(result.Value!, cancellationToken);

            return ApiResult.Success();
        }

        // Local sign-out always succeeds; server revocation is best effort (e.g. offline)
        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            var tokens = await tokenStore.GetAsync(cancellationToken);
            if (tokens is not null)
            {
                var result = await apiClient.LogoutAsync(tokens.RefreshToken, cancellationToken);
                if (!result.IsSuccess)
                {
                    logger.LogWarning("Server-side logout failed with {ErrorCode}", result.Error.ErrorCode);
                }
            }

            await tokenStore.ClearAsync(cancellationToken);
        }

        private static ApiResult SignInFailed() =>
            ApiResult.Failure(new ApiError(null, SignInFailedCode, ErrorMessageProvider.GetMessage(SignInFailedCode, null), null));

        private static bool TryReadUnixTime(IReadOnlyDictionary<string, string> properties, string key, out DateTimeOffset value)
        {
            value = default;

            if (!properties.TryGetValue(key, out var raw)
                || !long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seconds))
            {
                return false;
            }

            value = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }
    }
}
