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
                new DateOnly(2026, 11, 2),
                new DateOnly(2026, 11, 8));
            var shiftId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.Method.Should().Be(HttpMethod.Post);
                message.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts");
                // Days travel as plain dates: no time, no offset to shift them across time zones
                (await message.Content!.ReadAsStringAsync()).Should()
                    .Contain("\"startDate\":\"2026-11-02\"").And.Contain("\"endDate\":\"2026-11-08\"");
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
                $$"""[{"id":"{{shiftId}}","houseId":"{{houseId}}","assignedTenantId":"{{Guid.NewGuid()}}","assignedTenantName":"Salvo","assignedRoomId":"{{Guid.NewGuid()}}","assignedRoomName":"Bagno","startDate":"2026-10-30","endDate":"2026-11-06","dueDate":"2026-11-06T23:00:00Z","isShiftFullyCompleted":false,"checklist":[{"id":"{{itemId}}","description":"Lavare il pavimento","isCompleted":false,"completedAt":null}]}]""");
            var client = CreateClient(handler);

            var result = await client.GetHouseShiftsAsync(houseId);

            result.IsSuccess.Should().BeTrue();
            var shift = result.Value.Should().ContainSingle().Subject;
            shift.Checklist.Should().ContainSingle(item =>
                item.Id == itemId && item.Description == "Lavare il pavimento" && !item.IsCompleted);
            shift.StartDate.Should().Be(new DateOnly(2026, 10, 30));
            shift.EndDate.Should().Be(new DateOnly(2026, 11, 6));
            shift.DueDate.Should().Be(new DateTime(2026, 11, 6, 23, 0, 0, DateTimeKind.Utc));
            shift.DueDate.Kind.Should().Be(DateTimeKind.Utc);
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
