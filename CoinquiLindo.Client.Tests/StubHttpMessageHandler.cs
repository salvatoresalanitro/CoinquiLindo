using System.Net;
using System.Text;

namespace CoinquiLindo.Client.Tests
{
    internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return handler(request, cancellationToken);
        }

        public static StubHttpMessageHandler Returning(HttpStatusCode status, string? body = null, string mediaType = "application/json") =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = body is null ? null : new StringContent(body, Encoding.UTF8, mediaType)
            }));

        public static StubHttpMessageHandler ReturningProblem(HttpStatusCode status, string? errorCode, string traceId = "00-trace-01") =>
            Returning(status,
                $$"""
                {
                  "type": "https://tools.ietf.org/html/rfc9110",
                  "title": "Error",
                  "status": {{(int)status}},
                  "detail": "Internal developer-oriented detail",
                  {{(errorCode is null ? "" : $"\"errorCode\": \"{errorCode}\",")}}
                  "traceId": "{{traceId}}"
                }
                """,
                "application/problem+json");

        public static StubHttpMessageHandler Throwing(Exception exception) =>
            new((_, _) => Task.FromException<HttpResponseMessage>(exception));
    }
}
