using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class HouseAuthorizationTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        [Fact]
        public async Task HouseEndpoints_Should_Return401_When_Anonymous()
        {
            var client = factory.CreateClient();

            var create = await client.PostAsJsonAsync("/api/houses", new CreateHouseRequest("Casa"));
            var get = await client.GetAsync($"/api/houses/{Guid.NewGuid()}");
            var shift = await client.PostAsJsonAsync($"/api/houses/{Guid.NewGuid()}/shifts",
                new AssignShiftRequest(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddDays(7)));

            (await create.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            (await get.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            (await shift.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
        }

        [Fact]
        public async Task CreateHouse_Should_MakeCreatorFirstMember()
        {
            var client = await factory.CreateAuthenticatedClientAsync(displayName: "Salvo");

            var houseId = await client.CreateHouseAsync("Casa Milano");

            var house = await client.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}");
            house!.Tenants.Should().ContainSingle(t => t.Name == "Salvo");

            var me = await client.GetFromJsonAsync<MeResponse>("/api/me");
            me!.Houses.Should().ContainSingle(h => h.Id == houseId && h.Name == "Casa Milano");
        }

        [Fact]
        public async Task HouseEndpoints_Should_Return403Problem_When_UserIsNotMember()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync();
            var stranger = await factory.CreateAuthenticatedClientAsync();

            var get = await stranger.GetAsync($"/api/houses/{houseId}");
            var addRoom = await stranger.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest("Cucina", null));
            var addTenant = await stranger.PostAsJsonAsync($"/api/houses/{houseId}/tenants", new AddTenantRequest("Intruso"));

            (await get.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            (await addRoom.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            (await addTenant.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");

            var ownerView = await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}");
            ownerView!.Rooms.Should().BeEmpty();
            ownerView.Tenants.Should().ContainSingle();
        }
    }
}
