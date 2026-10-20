using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    internal sealed record Problem(int Status, string? Title, string? Detail, string? ErrorCode, string? TraceId, string ContentType, string RawBody);

    internal static class ProblemDetailsReader
    {
        public static async Task<Problem> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expectedStatus)
        {
            response.StatusCode.Should().Be(expectedStatus);

            var body = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            return new Problem(
                root.GetProperty("status").GetInt32(),
                GetString(root, "title"),
                GetString(root, "detail"),
                GetString(root, "errorCode"),
                GetString(root, "traceId"),
                response.Content.Headers.ContentType?.MediaType ?? string.Empty,
                body);
        }

        private static string? GetString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) ? value.GetString() : null;
    }
}
