using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Client.Tests
{
    public class AuthTokenHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 10, 29, 10, 0, 0, TimeSpan.Zero);

        private readonly MutableTimeProvider _clock = new(Now);
        private readonly ConcurrentQueue<(string Path, string? Bearer, string? Body)> _sent = new();
        private int _refreshCalls;

        private static AuthResponse Tokens(string access, string refresh, DateTimeOffset accessExpiresAt) =>
            new(access, accessExpiresAt, refresh, accessExpiresAt.AddDays(30));

        private HttpClient CreateClient(ITokenStore store, Func<HttpRequestMessage, Task<HttpResponseMessage>>? refreshResponder = null)
        {
            refreshResponder ??= _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(Tokens("new-access", "new-refresh", Now.AddMinutes(15)))
            });

            var inner = new StubHttpMessageHandler(async (request, _) =>
            {
                var body = request.Content is null ? null : await request.Content.ReadAsStringAsync();
                _sent.Enqueue((request.RequestUri!.AbsolutePath, request.Headers.Authorization?.Parameter, body));

                if (request.RequestUri.AbsolutePath == "/api/auth/refresh")
                {
                    Interlocked.Increment(ref _refreshCalls);
                    return await refreshResponder(request);
                }

                return new HttpResponseMessage(HttpStatusCode.OK);
            });

            return new HttpClient(new AuthTokenHandler(store, _clock) { InnerHandler = inner })
            {
                BaseAddress = new Uri("http://localhost")
            };
        }

        [Fact]
        public async Task Should_AttachBearer_When_AccessTokenIsValid()
        {
            var store = new InMemoryTokenStore(Tokens("valid-access", "refresh", Now.AddMinutes(10)));
            var client = CreateClient(store);

            await client.GetAsync("/api/me");

            _sent.Should().ContainSingle().Which.Bearer.Should().Be("valid-access");
            _refreshCalls.Should().Be(0);
        }

        [Fact]
        public async Task Should_SendAnonymously_When_NoSession()
        {
            var client = CreateClient(new InMemoryTokenStore());

            await client.GetAsync("/api/me");

            _sent.Should().ContainSingle().Which.Bearer.Should().BeNull();
        }

        [Fact]
        public async Task Should_RefreshAndSave_When_AccessTokenIsAboutToExpire()
        {
            var store = new InMemoryTokenStore(Tokens("old-access", "old-refresh", Now.AddSeconds(10)));
            var client = CreateClient(store);

            await client.GetAsync("/api/me");

            _refreshCalls.Should().Be(1);
            _sent.First().Body.Should().Contain("old-refresh");
            _sent.Last().Should().Match<(string Path, string? Bearer, string? Body)>(r => r.Path == "/api/me" && r.Bearer == "new-access");
            store.Current!.RefreshToken.Should().Be("new-refresh");
        }

        [Fact]
        public async Task Should_RefreshOnlyOnce_When_ConcurrentRequestsFindExpiredToken()
        {
            var store = new InMemoryTokenStore(Tokens("old-access", "old-refresh", Now.AddMinutes(-1)));
            var client = CreateClient(store, async _ =>
            {
                await Task.Delay(50);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(Tokens("new-access", "new-refresh", Now.AddMinutes(15)))
                };
            });

            await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => client.GetAsync("/api/me")));

            _refreshCalls.Should().Be(1);
            store.SaveCount.Should().Be(1);
            _sent.Where(r => r.Path == "/api/me").Should().HaveCount(5)
                .And.OnlyContain(r => r.Bearer == "new-access");
        }

        [Fact]
        public async Task Should_ClearSession_When_RefreshTokenIsRejected()
        {
            var store = new InMemoryTokenStore(Tokens("old-access", "old-refresh", Now.AddMinutes(-1)));
            var client = CreateClient(store, _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

            await client.GetAsync("/api/me");

            store.Current.Should().BeNull();
            _sent.Last().Should().Match<(string Path, string? Bearer, string? Body)>(r => r.Path == "/api/me" && r.Bearer == null);
        }

        [Fact]
        public async Task Should_KeepSession_When_RefreshFailsForNetworkReasons()
        {
            var tokens = Tokens("old-access", "old-refresh", Now.AddSeconds(5));
            var store = new InMemoryTokenStore(tokens);
            var client = CreateClient(store, _ => Task.FromException<HttpResponseMessage>(new HttpRequestException("offline")));

            await client.GetAsync("/api/me");

            store.Current.Should().Be(tokens);
            _sent.Last().Bearer.Should().Be("old-access");
        }

        [Theory]
        [InlineData("/api/auth/refresh")]
        [InlineData("/api/auth/logout")]
        [InlineData("/api/auth/dev-login")]
        public async Task Should_NotAttachBearerNorRefresh_ForAuthEndpoints(string path)
        {
            var store = new InMemoryTokenStore(Tokens("old-access", "old-refresh", Now.AddMinutes(-1)));
            var client = CreateClient(store);

            await client.PostAsync(path, null);

            _sent.Should().ContainSingle().Which.Bearer.Should().BeNull();
            store.Current!.AccessToken.Should().Be("old-access");
        }
    }
}
