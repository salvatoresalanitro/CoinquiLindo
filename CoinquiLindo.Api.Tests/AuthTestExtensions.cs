using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoinquiLindo.Api.Tests
{
    internal static class AuthTestExtensions
    {
        public static async Task<AuthResponse> DevLoginAsync(this HttpClient client, string email, string displayName = "Salvo")
        {
            var response = await client.PostAsJsonAsync("/api/auth/dev-login", new DevLoginRequest(email, displayName));
            response.EnsureSuccessStatusCode();

            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        }

        public static async Task<HttpClient> CreateAuthenticatedClientAsync(
            this WebApplicationFactory<Program> factory, string? email = null, string displayName = "Salvo")
        {
            var client = factory.CreateClient();
            var auth = await client.DevLoginAsync(email ?? $"user-{Guid.NewGuid():N}@gmail.com", displayName);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);

            return client;
        }

        public static async Task<Guid> CreateHouseAsync(this HttpClient client, string name = "Casa Test")
        {
            var response = await client.PostAsJsonAsync("/api/houses", new CreateHouseRequest(name));
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<Guid>();
        }
    }
}
