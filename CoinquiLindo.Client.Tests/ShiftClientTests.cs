using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class ShiftClientTests
    {
        [Fact]
        public async Task AssignShift_Should_SendRequestToHouseShiftsRoute()
        {
            var houseId = Guid.NewGuid();
            var request = new AssignShiftRequest(
                Guid.NewGuid(),
                Guid.NewGuid(),
                DateTime.UtcNow.Date,
                DateTime.UtcNow.Date.AddDays(7));
            var shiftId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.Method.Should().Be(HttpMethod.Post);
                message.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts");
                (await message.Content!.ReadFromJsonAsync<AssignShiftRequest>()).Should().BeEquivalentTo(request);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(shiftId)
                };
            });
            var client = CreateClient(handler);

            var result = await client.AssignShiftAsync(houseId, request);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(shiftId);
        }

        [Fact]
        public async Task GetHouseShifts_Should_ReadShiftAndChecklistResponse()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK,
                $$"""[{"id":"{{shiftId}}","houseId":"{{houseId}}","assignedTenantId":"{{Guid.NewGuid()}}","assignedTenantName":"Salvo","assignedRoomId":"{{Guid.NewGuid()}}","assignedRoomName":"Bagno","startDate":"2026-10-30T00:00:00Z","endDate":"2026-11-06T00:00:00Z","isShiftFullyCompleted":false,"checklist":[{"id":"{{itemId}}","description":"Lavare il pavimento","isCompleted":false,"completedAt":null}]}]""");
            var client = CreateClient(handler);

            var result = await client.GetHouseShiftsAsync(houseId);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle().Which.Checklist.Should().ContainSingle(item =>
                item.Id == itemId && item.Description == "Lavare il pavimento" && !item.IsCompleted);
        }

        [Fact]
        public async Task CompleteShiftChecklistItem_Should_SendPutToHouseScopedRoute()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var itemId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Put);
                request.RequestUri!.PathAndQuery.Should().Be(
                    $"/api/houses/{houseId}/shifts/{shiftId}/checklist/{itemId}");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });
            var client = CreateClient(handler);

            var result = await client.CompleteShiftChecklistItemAsync(houseId, shiftId, itemId);

            result.IsSuccess.Should().BeTrue();
        }

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
