using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class ShiftEndpointsTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        [Fact]
        public async Task HouseShifts_Should_ListAndCompleteChecklistItems()
        {
            var client = await factory.CreateAuthenticatedClientAsync(displayName: "Salvo");
            var houseId = await client.CreateHouseAsync("Casa turni", "America/New_York");
            var house = (await client.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var roomResponse = await client.PostAsJsonAsync(
                $"/api/houses/{houseId}/rooms",
                new AddRoomRequest("Bagno", ["Lavare il pavimento"]));
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();
            var startDate = new DateOnly(2026, 11, 2);
            var assignResponse = await client.PostAsJsonAsync(
                $"/api/houses/{houseId}/shifts",
                new AssignShiftRequest(house.Tenants.Single().Id, roomId, startDate, startDate.AddDays(6)));
            var shiftId = await assignResponse.Content.ReadFromJsonAsync<Guid>();

            var shifts = (await client.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{houseId}/shifts"))!;
            var shift = shifts.Should().ContainSingle().Which;
            var item = shift.Checklist.Should().ContainSingle().Which;
            shift.Id.Should().Be(shiftId);
            shift.HouseId.Should().Be(houseId);
            shift.StartDate.Should().Be(startDate);
            shift.EndDate.Should().Be(new DateOnly(2026, 11, 8));
            // End of 08/11 in New York, sent as a UTC instant so every client converts it correctly
            shift.DueDate.Should().Be(new DateTime(2026, 11, 9, 5, 0, 0, DateTimeKind.Utc));
            shift.DueDate.Kind.Should().Be(DateTimeKind.Utc);
            shift.AssignedTenantName.Should().Be("Salvo");
            shift.AssignedRoomName.Should().Be("Bagno");
            item.Description.Should().Be("Lavare il pavimento");
            item.IsCompleted.Should().BeFalse();

            var completeResponse = await client.PutAsync(
                $"/api/houses/{houseId}/shifts/{shiftId}/checklist/{item.Id}",
                content: null);

            completeResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var updatedShifts = (await client.GetFromJsonAsync<List<ShiftResponse>>($"/api/houses/{houseId}/shifts"))!;
            updatedShifts!.Single().Checklist.Single().IsCompleted.Should().BeTrue();
            updatedShifts.Single().Checklist.Single().CompletedAt.Should().NotBeNull();
            updatedShifts.Single().IsShiftFullyCompleted.Should().BeTrue();
        }

        [Theory]
        [InlineData(0, HttpStatusCode.OK)]
        [InlineData(-1, HttpStatusCode.BadRequest)]
        public async Task AssignShift_Should_AcceptSingleDayShift_AndRejectEndBeforeStart(int endOffsetDays, HttpStatusCode expected)
        {
            var client = await factory.CreateAuthenticatedClientAsync();
            var houseId = await client.CreateHouseAsync("Casa date");
            var house = (await client.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var roomResponse = await client.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest("Bagno", null));
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();
            var startDate = new DateOnly(2026, 11, 2);

            var response = await client.PostAsJsonAsync($"/api/houses/{houseId}/shifts",
                new AssignShiftRequest(house.Tenants.Single().Id, roomId, startDate, startDate.AddDays(endOffsetDays)));

            response.StatusCode.Should().Be(expected);
            if (expected == HttpStatusCode.BadRequest)
            {
                (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("Shift.InvalidDateRange");
            }
        }

        [Fact]
        public async Task CreateHouse_Should_Return400_When_TimeZoneIsUnknown()
        {
            var client = await factory.CreateAuthenticatedClientAsync();

            var response = await client.PostAsJsonAsync("/api/houses", new CreateHouseRequest("Casa", "Mars/Olympus_Mons"));

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("House.TimeZoneInvalid");
        }

        [Fact]
        public async Task HouseShifts_Should_RequireHouseMembership()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync("Casa privata");
            var outsider = await factory.CreateAuthenticatedClientAsync();

            var response = await outsider.GetAsync($"/api/houses/{houseId}/shifts");

            (await response.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
        }

        [Fact]
        public async Task CompleteChecklistItem_Should_NotAccessShiftFromAnotherHouse()
        {
            var client = await factory.CreateAuthenticatedClientAsync();
            var houseId = await client.CreateHouseAsync("Casa A");
            var otherHouseId = await client.CreateHouseAsync("Casa B");
            var otherHouse = (await client.GetFromJsonAsync<HouseResponse>($"/api/houses/{otherHouseId}"))!;
            var roomResponse = await client.PostAsJsonAsync(
                $"/api/houses/{otherHouseId}/rooms",
                new AddRoomRequest("Bagno", ["Lavare il pavimento"]));
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();
            var startDate = new DateOnly(2026, 11, 2);
            var assignResponse = await client.PostAsJsonAsync(
                $"/api/houses/{otherHouseId}/shifts",
                new AssignShiftRequest(otherHouse.Tenants.Single().Id, roomId, startDate, startDate.AddDays(7)));
            var shiftId = await assignResponse.Content.ReadFromJsonAsync<Guid>();
            var foreignShift = (await client.GetFromJsonAsync<List<ShiftResponse>>(
                $"/api/houses/{otherHouseId}/shifts"))!.Single();

            var response = await client.PutAsync(
                $"/api/houses/{houseId}/shifts/{shiftId}/checklist/{foreignShift.Checklist.Single().Id}",
                content: null);

            (await response.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Shift.NotFound");
        }
    }
}
