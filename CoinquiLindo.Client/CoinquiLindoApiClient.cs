using System.Net.Http.Headers;
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

        public async Task<ApiResult<InvitePreviewResponse>> GetInvitePreviewAsync(string inviteCode, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<InvitePreviewResponse>(
                ct => httpClient.GetAsync($"/api/houses/invites/{Uri.EscapeDataString(inviteCode)}", ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<InvitePreviewResponse>.Success(value!) : ApiResult<InvitePreviewResponse>.Failure(error);
        }

        public async Task<ApiResult<Guid>> JoinHouseAsync(string inviteCode, Guid? claimTenantId, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<Guid>(
                ct => httpClient.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, claimTenantId), ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<Guid>.Success(value) : ApiResult<Guid>.Failure(error);
        }

        public async Task<ApiResult<InviteCodeResponse>> RegenerateInviteCodeAsync(Guid houseId, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<InviteCodeResponse>(
                ct => httpClient.PostAsync($"/api/houses/{houseId}/invite-code", null, ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<InviteCodeResponse>.Success(value!) : ApiResult<InviteCodeResponse>.Failure(error);
        }

        public async Task<ApiResult<Guid>> AddRoomAsync(Guid houseId, string name, List<string> cleaningTasks, CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<Guid>(
                ct => httpClient.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest(name, cleaningTasks), ct), readBody: true, cancellationToken);

            return error is null ? ApiResult<Guid>.Success(value) : ApiResult<Guid>.Failure(error);
        }

        public async Task<ApiResult<Guid>> AssignShiftAsync(
            Guid houseId,
            AssignShiftRequest request,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<Guid>(
                ct => httpClient.PostAsJsonAsync($"/api/houses/{houseId}/shifts", request, ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<Guid>.Success(value) : ApiResult<Guid>.Failure(error);
        }

        public async Task<ApiResult<List<ShiftResponse>>> GetHouseShiftsAsync(
            Guid houseId,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<List<ShiftResponse>>(
                ct => httpClient.GetAsync($"/api/houses/{houseId}/shifts", ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<List<ShiftResponse>>.Success(value!) : ApiResult<List<ShiftResponse>>.Failure(error);
        }

        public async Task<ApiResult> CompleteShiftChecklistItemAsync(
            Guid houseId,
            Guid shiftId,
            Guid checklistItemId,
            CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PutAsync(
                    $"/api/houses/{houseId}/shifts/{shiftId}/checklist/{checklistItemId}",
                    content: null,
                    ct),
                readBody: false,
                cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult<ReportResponse>> CreateShiftReportAsync(
            Guid houseId,
            Guid shiftId,
            string description,
            byte[] photo,
            string photoContentType,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<ReportResponse>(
                async ct =>
                {
                    using var form = new MultipartFormDataContent();
                    form.Add(new StringContent(description), ReportForm.DescriptionField);
                    var photoContent = new ByteArrayContent(photo);
                    photoContent.Headers.ContentType = new MediaTypeHeaderValue(photoContentType);
                    // The server ignores the name and detects the format from the bytes
                    form.Add(photoContent, ReportForm.PhotoField, "camera-photo");
                    return await httpClient.PostAsync($"/api/houses/{houseId}/shifts/{shiftId}/reports", form, ct);
                },
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<ReportResponse>.Success(value!) : ApiResult<ReportResponse>.Failure(error);
        }

        public async Task<ApiResult<List<ReportResponse>>> GetShiftReportsAsync(
            Guid houseId,
            Guid shiftId,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<List<ReportResponse>>(
                ct => httpClient.GetAsync($"/api/houses/{houseId}/shifts/{shiftId}/reports", ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<List<ReportResponse>>.Success(value!) : ApiResult<List<ReportResponse>>.Failure(error);
        }

        public async Task<ApiResult<ReportResponse>> CastReportVoteAsync(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            ReportVoteValue vote,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<ReportResponse>(
                ct => httpClient.PutAsJsonAsync(
                    $"/api/houses/{houseId}/shifts/{shiftId}/reports/{reportId}/vote", new CastReportVoteRequest(vote), ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<ReportResponse>.Success(value!) : ApiResult<ReportResponse>.Failure(error);
        }

        public async Task<ApiResult<ReportResponse>> RemoveReportVoteAsync(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<ReportResponse>(
                ct => httpClient.DeleteAsync($"/api/houses/{houseId}/shifts/{shiftId}/reports/{reportId}/vote", ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<ReportResponse>.Success(value!) : ApiResult<ReportResponse>.Failure(error);
        }

        public async Task<ApiResult<List<NotificationResponse>>> GetNotificationsAsync(
            bool unreadOnly = false,
            int take = 50,
            CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<List<NotificationResponse>>(
                ct => httpClient.GetAsync($"/api/me/notifications?unreadOnly={(unreadOnly ? "true" : "false")}&take={take}", ct),
                readBody: true,
                cancellationToken);

            return error is null
                ? ApiResult<List<NotificationResponse>>.Success(value!)
                : ApiResult<List<NotificationResponse>>.Failure(error);
        }

        public async Task<ApiResult<int>> GetUnreadNotificationsCountAsync(CancellationToken cancellationToken = default)
        {
            var (value, error) = await SendAsync<UnreadNotificationsResponse>(
                ct => httpClient.GetAsync("/api/me/notifications/unread-count", ct),
                readBody: true,
                cancellationToken);

            return error is null ? ApiResult<int>.Success(value!.Count) : ApiResult<int>.Failure(error);
        }

        public async Task<ApiResult> MarkNotificationAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PutAsync($"/api/me/notifications/{notificationId}/read", null, ct),
                readBody: false,
                cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult> MarkAllNotificationsAsReadAsync(CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PutAsync("/api/me/notifications/read-all", null, ct),
                readBody: false,
                cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult> RegisterPushDeviceAsync(string token, PushPlatformValue platform, CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PutAsJsonAsync("/api/me/devices", new RegisterPushDeviceRequest(token, platform), ct),
                readBody: false,
                cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult> UnregisterPushDeviceAsync(string token, CancellationToken cancellationToken = default)
        {
            var (_, error) = await SendAsync<object>(
                ct => httpClient.PostAsJsonAsync("/api/me/devices/unregister", new UnregisterPushDeviceRequest(token), ct),
                readBody: false,
                cancellationToken);

            return error is null ? ApiResult.Success() : ApiResult.Failure(error);
        }

        public async Task<ApiResult<ReportPhotoData>> GetReportPhotoAsync(string photoUrl, CancellationToken cancellationToken = default)
        {
            // The bearer token is attached to every request: only API-relative paths are allowed
            if (!photoUrl.StartsWith("/api/", StringComparison.Ordinal)
                || !Uri.TryCreate(photoUrl, UriKind.Relative, out var relativeUri))
            {
                logger.LogWarning("Refused to download a report photo outside the API: {PhotoUrl}", photoUrl);
                return ApiResult<ReportPhotoData>.Failure(new ApiError(null, null, ErrorMessageProvider.DefaultMessage, null));
            }

            var (value, error) = await SendAsync<ReportPhotoData>(
                ct => httpClient.GetAsync(relativeUri, ct),
                async (content, ct) => new ReportPhotoData(
                    await content.ReadAsByteArrayAsync(ct),
                    content.Headers.ContentType?.MediaType ?? "application/octet-stream"),
                cancellationToken);

            return error is null ? ApiResult<ReportPhotoData>.Success(value!) : ApiResult<ReportPhotoData>.Failure(error);
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

        private Task<(T? Value, ApiError? Error)> SendAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> send, bool readBody, CancellationToken cancellationToken) =>
            SendAsync<T>(send, readBody ? (content, ct) => content.ReadFromJsonAsync<T>(ct) : null, cancellationToken);

        private async Task<(T? Value, ApiError? Error)> SendAsync<T>(
            Func<CancellationToken, Task<HttpResponseMessage>> send,
            Func<HttpContent, CancellationToken, Task<T?>>? read,
            CancellationToken cancellationToken)
        {
            try
            {
                using var response = await send(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return (default, await ReadErrorAsync(response, cancellationToken));
                }

                if (read is null)
                {
                    return (default, null);
                }

                var value = await read(response.Content, cancellationToken);
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
