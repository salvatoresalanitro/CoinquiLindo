using System.Net;
using System.Net.Http.Json;
using System.Text;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class ErrorHandlingTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>, IAsyncLifetime
    {
        private HttpClient _client = null!;

        public async Task InitializeAsync() => _client = await factory.CreateAuthenticatedClientAsync();

        public Task DisposeAsync() => Task.CompletedTask;

        [Fact]
        public async Task AddTenant_Should_Return404Problem_When_HouseDoesNotExist()
        {
            var response = await _client.PostAsJsonAsync($"/api/houses/{Guid.NewGuid()}/tenants", new AddTenantRequest("Salvo"));

            var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
            problem.ContentType.Should().Be("application/problem+json");
            problem.Status.Should().Be(404);
            problem.ErrorCode.Should().Be("House.NotFound");
            problem.TraceId.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public async Task AddRoom_Should_Return409Problem_When_RoomNameAlreadyExists()
        {
            var houseId = await _client.CreateHouseAsync();
            await _client.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest("Cucina", null));

            var response = await _client.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest(" cucina ", null));

            var problem = await response.ReadProblemAsync(HttpStatusCode.Conflict);
            problem.ErrorCode.Should().Be("House.RoomNameAlreadyExists");
        }

        [Fact]
        public async Task CreateHouse_Should_Return400Problem_When_NameIsBlank()
        {
            var response = await _client.PostAsJsonAsync("/api/houses", new CreateHouseRequest(""));

            var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
            problem.ErrorCode.Should().Be("House.NameRequired");
        }

        [Fact]
        public async Task CreateHouse_Should_Return400Problem_When_BodyIsMalformed()
        {
            var content = new StringContent("{ not-json", Encoding.UTF8, "application/json");

            var response = await _client.PostAsync("/api/houses", content);

            var problem = await response.ReadProblemAsync(HttpStatusCode.BadRequest);
            problem.ContentType.Should().Be("application/problem+json");
        }

        [Fact]
        public async Task AssignShift_Should_Return404Problem_When_TenantNotInHouse()
        {
            var houseId = await _client.CreateHouseAsync();
            var request = new AssignShiftRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

            var response = await _client.PostAsJsonAsync($"/api/houses/{houseId}/shifts", request);

            var problem = await response.ReadProblemAsync(HttpStatusCode.NotFound);
            problem.ErrorCode.Should().Be("Tenant.NotFound");
        }
    }
}
