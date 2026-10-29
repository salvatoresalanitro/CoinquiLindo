using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoinquiLindo.Api.Tests
{
    public class AuthEndpointsTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        private readonly HttpClient _client = factory.CreateClient();

        [Fact]
        public async Task DevLogin_Should_ReturnTokens_UsableOnMe()
        {
            var auth = await _client.DevLoginAsync("salvo.me@gmail.com", "Salvo");

            auth.AccessToken.Should().NotBeNullOrWhiteSpace();
            auth.RefreshToken.Should().NotBeNullOrWhiteSpace();
            auth.AccessTokenExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
            auth.RefreshTokenExpiresAt.Should().BeAfter(auth.AccessTokenExpiresAt);

            var me = await GetMeAsync(auth.AccessToken);
            me.Email.Should().Be("salvo.me@gmail.com");
            me.DisplayName.Should().Be("Salvo");
            me.Houses.Should().BeEmpty();
        }

        [Fact]
        public async Task DevLogin_Should_ReuseSameUser_OnSubsequentLogins()
        {
            var first = await _client.DevLoginAsync("same.user@gmail.com", "Salvo");
            var second = await _client.DevLoginAsync("same.user@gmail.com", "Salvo Renamed");

            var firstMe = await GetMeAsync(first.AccessToken);
            var secondMe = await GetMeAsync(second.AccessToken);

            secondMe.Id.Should().Be(firstMe.Id);
            secondMe.DisplayName.Should().Be("Salvo Renamed");
        }

        [Fact]
        public async Task DevLogin_Should_NotExist_OutsideDevelopment()
        {
            var client = factory.WithWebHostBuilder(b => b.UseEnvironment("Production")).CreateClient();

            var response = await client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest("prod@gmail.com", "Prod"));

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task Api_Should_Work_When_GoogleIsNotConfigured()
        {
            var unconfigured = factory.WithWebHostBuilder(b =>
            {
                b.UseSetting("Auth:Google:ClientId", "");
                b.UseSetting("Auth:Google:ClientSecret", "");
            });
            var client = unconfigured.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var auth = await client.DevLoginAsync("no.google@gmail.com");
            var google = await client.GetAsync("/api/auth/google");

            auth.AccessToken.Should().NotBeNullOrWhiteSpace();
            google.StatusCode.Should().Be(HttpStatusCode.Redirect);
            google.Headers.Location!.OriginalString.Should().Be($"{CoinquiLindoApiFactory.MobileCallbackUri}#error=google_not_configured");
        }

        [Fact]
        public async Task Me_Should_Return401Problem_When_TokenIsMissing()
        {
            var response = await _client.GetAsync("/api/me");

            var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
            problem.ContentType.Should().Be("application/problem+json");
            problem.ErrorCode.Should().Be("Auth.Unauthorized");
            problem.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task Me_Should_Return401Problem_When_TokenIsInvalid()
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

            var response = await _client.SendAsync(request);

            var problem = await response.ReadProblemAsync(HttpStatusCode.Unauthorized);
            problem.ErrorCode.Should().Be("Auth.Unauthorized");
        }

        [Fact]
        public async Task Refresh_Should_RotateTokens_And_RejectReusedToken()
        {
            var auth = await _client.DevLoginAsync("refresh.user@gmail.com");

            var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var rotated = (await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>())!;
            rotated.RefreshToken.Should().NotBe(auth.RefreshToken);
            (await GetMeAsync(rotated.AccessToken)).Email.Should().Be("refresh.user@gmail.com");

            var reuseResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));

            var problem = await reuseResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
            problem.ErrorCode.Should().Be("Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task Logout_Should_RevokeRefreshToken()
        {
            var auth = await _client.DevLoginAsync("logout.user@gmail.com");

            var logoutResponse = await _client.PostAsJsonAsync("/api/auth/logout", new RefreshTokenRequest(auth.RefreshToken));
            logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var refreshResponse = await _client.PostAsJsonAsync("/api/auth/refresh", new RefreshTokenRequest(auth.RefreshToken));
            var problem = await refreshResponse.ReadProblemAsync(HttpStatusCode.Unauthorized);
            problem.ErrorCode.Should().Be("Auth.InvalidRefreshToken");
        }

        [Fact]
        public async Task GoogleLogin_Should_RedirectToGoogleConsent()
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync("/api/auth/google");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            response.Headers.Location!.Host.Should().Be("accounts.google.com");
            response.Headers.Location.Query.Should().Contain("client_id=test-client-id.apps.googleusercontent.com");
        }

        [Fact]
        public async Task GoogleComplete_Should_RedirectToAppWithError_When_ExternalLoginIsMissing()
        {
            var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

            var response = await client.GetAsync("/api/auth/google/complete");

            response.StatusCode.Should().Be(HttpStatusCode.Redirect);
            var location = response.Headers.Location!.OriginalString;
            location.Should().StartWith($"{CoinquiLindoApiFactory.MobileCallbackUri}#");
            location.Should().Contain("error=");
            location.Should().NotContain("access_token");
        }

        private async Task<MeResponse> GetMeAsync(string accessToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await _client.SendAsync(request);
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            return (await response.Content.ReadFromJsonAsync<MeResponse>())!;
        }
    }
}
