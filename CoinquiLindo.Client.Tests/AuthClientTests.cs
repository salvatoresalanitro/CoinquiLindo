using System.Net;
using System.Text.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class AuthClientTests
    {
        private static readonly DateTimeOffset AccessExpiry = new(2026, 10, 29, 10, 15, 0, TimeSpan.Zero);
        private static readonly DateTimeOffset RefreshExpiry = new(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);

        private readonly InMemoryTokenStore _store = new();

        private AuthClient CreateAuthClient(StubHttpMessageHandler handler) =>
            new(new CoinquiLindoApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, NullLogger<CoinquiLindoApiClient>.Instance),
                _store,
                NullLogger<AuthClient>.Instance);

        [Fact]
        public async Task CompleteGoogleSignIn_Should_SaveTokens_FromCallback()
        {
            var authClient = CreateAuthClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK));
            var callback = new Dictionary<string, string>
            {
                ["access_token"] = "access",
                ["access_token_expires_at"] = AccessExpiry.ToUnixTimeSeconds().ToString(),
                ["refresh_token"] = "refresh",
                ["refresh_token_expires_at"] = RefreshExpiry.ToUnixTimeSeconds().ToString()
            };

            var result = await authClient.CompleteGoogleSignInAsync(callback);

            result.IsSuccess.Should().BeTrue();
            _store.Current.Should().Be(new AuthResponse("access", AccessExpiry, "refresh", RefreshExpiry));
            (await authClient.IsSignedInAsync()).Should().BeTrue();
        }

        [Fact]
        public async Task CompleteGoogleSignIn_Should_Fail_When_CallbackContainsError()
        {
            var authClient = CreateAuthClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK));

            var result = await authClient.CompleteGoogleSignInAsync(new Dictionary<string, string> { ["error"] = "access_denied" });

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be(AuthClient.SignInFailedCode);
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage(AuthClient.SignInFailedCode, null));
            _store.Current.Should().BeNull();
        }

        [Theory]
        [InlineData("refresh_token")]
        [InlineData("access_token_expires_at")]
        public async Task CompleteGoogleSignIn_Should_Fail_When_CallbackIsIncomplete(string missingKey)
        {
            var authClient = CreateAuthClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK));
            var callback = new Dictionary<string, string>
            {
                ["access_token"] = "access",
                ["access_token_expires_at"] = AccessExpiry.ToUnixTimeSeconds().ToString(),
                ["refresh_token"] = "refresh",
                ["refresh_token_expires_at"] = RefreshExpiry.ToUnixTimeSeconds().ToString()
            };
            callback.Remove(missingKey);

            var result = await authClient.CompleteGoogleSignInAsync(callback);

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be(AuthClient.SignInFailedCode);
            _store.Current.Should().BeNull();
        }

        [Fact]
        public async Task DevLogin_Should_SaveTokens_When_ApiSucceeds()
        {
            var tokens = new AuthResponse("access", AccessExpiry, "refresh", RefreshExpiry);
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, JsonSerializer.Serialize(tokens, JsonSerializerOptions.Web));
            var authClient = CreateAuthClient(handler);

            var result = await authClient.DevLoginAsync("salvo@gmail.com", "Salvo");

            result.IsSuccess.Should().BeTrue();
            handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/auth/dev-login");
            _store.Current.Should().Be(tokens);
        }

        [Fact]
        public async Task DevLogin_Should_NotSaveTokens_When_ApiFails()
        {
            var authClient = CreateAuthClient(StubHttpMessageHandler.ReturningProblem(HttpStatusCode.NotFound, null));

            var result = await authClient.DevLoginAsync("salvo@gmail.com", "Salvo");

            result.IsSuccess.Should().BeFalse();
            _store.Current.Should().BeNull();
        }

        [Fact]
        public async Task Logout_Should_RevokeOnServer_And_ClearStore()
        {
            await _store.SaveAsync(new AuthResponse("access", AccessExpiry, "my-refresh", RefreshExpiry));
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.NoContent);
            var authClient = CreateAuthClient(handler);

            await authClient.LogoutAsync();

            handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/auth/logout");
            (await handler.LastRequest.Content!.ReadAsStringAsync()).Should().Contain("my-refresh");
            _store.Current.Should().BeNull();
            (await authClient.IsSignedInAsync()).Should().BeFalse();
        }

        [Fact]
        public async Task Logout_Should_ClearStore_EvenWhenServerIsUnreachable()
        {
            await _store.SaveAsync(new AuthResponse("access", AccessExpiry, "my-refresh", RefreshExpiry));
            var authClient = CreateAuthClient(StubHttpMessageHandler.Throwing(new HttpRequestException("offline")));

            await authClient.LogoutAsync();

            _store.Current.Should().BeNull();
        }

        [Fact]
        public async Task GetMe_Should_ReturnProfile_When_ApiSucceeds()
        {
            var id = Guid.NewGuid();
            var houseId = Guid.NewGuid();
            var api = new CoinquiLindoApiClient(
                new HttpClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK,
                    $$"""{"id":"{{id}}","displayName":"Salvo","email":"salvo@gmail.com","houses":[{"id":"{{houseId}}","name":"Casa"}]}"""))
                { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);

            var result = await api.GetMeAsync();

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new MeResponse(id, "Salvo", "salvo@gmail.com", [new MyHouseResponse(houseId, "Casa")]));
        }
    }
}
