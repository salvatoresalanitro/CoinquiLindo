using System.Net.Http.Json;
using System.Text.Json;
using CoinquiLindo.Shared.Contracts;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Client
{
    public class CoinquiLindoApiClient(HttpClient httpClient, ILogger<CoinquiLindoApiClient> logger)
    {
        public async Task<ApiResult<Guid>> CreateHouseAsync(string name, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<Guid>(
                ct => httpClient.PostAsJsonAsync("/api/houses", new CreateHouseRequest(name), ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<Guid>.Success(value) : ApiResult<Guid>.Failure(error);
        }

        public async Task<ApiResult<HouseResponse>> GetHouseAsync(Guid houseId, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<HouseResponse>(
                ct => httpClient.GetAsync($"/api/houses/{houseId}", ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<HouseResponse>.Success(value!) : ApiResult<HouseResponse>.Failure(error);
        }

        public async Task<ApiResult> AddTenantAsync(Guid houseId, string tenantName, CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PostAsJsonAsync($"/api/houses/{houseId}/tenants", new AddTenantRequest(tenantName), ct), readBody: false, cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult<Guid>> AddRoomAsync(Guid houseId, string name, List<string> cleaningTasks, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<Guid>(
                ct => httpClient.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest(name, cleaningTasks), ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<Guid>.Success(value) : ApiResult<Guid>.Failure(error);
        }

        public async Task<ApiResult<MeResponse>> GetMeAsync(CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<MeResponse>(
                ct => httpClient.GetAsync("/api/me", ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<MeResponse>.Success(value!) : ApiResult<MeResponse>.Failure(error);
        }

        public async Task<ApiResult<AuthResponse>> DevLoginAsync(string email, string displayName, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<AuthResponse>(
                ct => httpClient.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest(email, displayName), ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<AuthResponse>.Success(value!) : ApiResult<AuthResponse>.Failure(error);
        }

        public async Task<ApiResult> LogoutAsync(string refreshToken, CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(refreshToken), ct), readBody: false, cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        private async Task<(T? Value, ApiError? Error)> SendAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> send, bool readBody, CancellationToken cancellationToken)
        {
            try
            {
                using var response = await send(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return (default, await ReadErrorAsync(response, cancellationToken));
                }

                if (!readBody)
                {
                    return (default, null);
                }

                var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
                return value is null ? (default, UnreadableResponse((int)response.StatusCode)) : (value, null);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // TaskCanceledException without caller cancellation means HttpClient timeout
                logger.LogWarning(ex, "API unreachable");
                return (default, new ApiError(null, ApiError.NetworkUnavailableCode,
                    ErrorMessageProvider.GetMessage(ApiError.NetworkUnavailableCode, null), null));
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Unable to deserialize a successful API response into {Type}", typeof(T).Name);
                return (default, UnreadableResponse(null));
            }
        }

        private async Task<ApiError> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            var status = (int)response.StatusCode;
            string? errorCode = null;
            string? traceId = null;

            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!string.IsNullOrWhiteSpace(body))
                {
                    using var json = JsonDocument.Parse(body);

                    if (json.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        errorCode = GetString(json.RootElement, "errorCode");
                        traceId = GetString(json.RootElement, "traceId");
                    }
                }
            }
            catch (JsonException)
            {
                // Non-ProblemDetails body (e.g. proxy HTML page): fall back to the status code
            }

            logger.LogWarning("API {Method} {Uri} failed with {Status} {ErrorCode} (traceId {TraceId})",
                response.RequestMessage?.Method, response.RequestMessage?.RequestUri, status, errorCode, traceId);

            return new ApiError(status, errorCode, ErrorMessageProvider.GetMessage(errorCode, status), traceId);
        }

        private static ApiError UnreadableResponse(int? status) =>
            new(status, null, ErrorMessageProvider.GetMessage(null, status ?? 500), null);

        private static string? GetString(JsonElement root, string propertyName) =>
            root.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
