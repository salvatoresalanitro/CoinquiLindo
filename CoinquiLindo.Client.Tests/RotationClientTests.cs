using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class RotationClientTests
    {
        private static readonly Guid HouseId = Guid.NewGuid();

        [Fact]
        public async Task GetRotationPlan_Should_ReturnNull_When_HouseHasNoPlan()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{HouseId}/rotation");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            var result = await CreateClient(handler).GetRotationPlanAsync(HouseId);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeNull();
        }

        [Fact]
        public async Task GetRotationPlan_Should_ReadPlanWithWeeks()
        {
            var planId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var roomId = Guid.NewGuid();
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK,
                $$"""{"id":"{{planId}}","startDate":"2026-11-02","tenants":[{"id":"{{tenantId}}","name":"Salvo"}],"rooms":[{"id":"{{roomId}}","name":"Bagno"}],"weeks":[{"index":0,"startDate":"2026-11-02","endDate":"2026-11-08","assignments":[{"tenantId":"{{tenantId}}","tenantName":"Salvo","roomId":"{{roomId}}","roomName":"Bagno"}],"restingTenants":[],"unassignedRooms":[]}]}""");

            var result = await CreateClient(handler).GetRotationPlanAsync(HouseId);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Id.Should().Be(planId);
            result.Value.StartDate.Should().Be(new DateOnly(2026, 11, 2));
            result.Value.Weeks.Single().Assignments.Single().RoomName.Should().Be("Bagno");
        }

        [Fact]
        public async Task GetRotationPlan_Should_ReturnError_ForProblemDetails()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.Forbidden, "House.AccessDenied");

            var result = await CreateClient(handler).GetRotationPlanAsync(HouseId);

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be("House.AccessDenied");
        }

        [Fact]
        public async Task SaveRotationPlan_Should_PutTheOrderedPlan()
        {
            var request = new SaveRotationPlanRequest([Guid.NewGuid(), Guid.NewGuid()], [Guid.NewGuid()], new DateOnly(2026, 11, 2));
            var response = new RotationPlanResponse(Guid.NewGuid(), request.StartDate, [], [], []);
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.Method.Should().Be(HttpMethod.Put);
                message.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{HouseId}/rotation");
                (await message.Content!.ReadAsStringAsync()).Should().Contain("\"startDate\":\"2026-11-02\"");
                (await message.Content!.ReadFromJsonAsync<SaveRotationPlanRequest>())!.TenantIds.Should().Equal(request.TenantIds);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(response) };
            });

            var result = await CreateClient(handler).SaveRotationPlanAsync(HouseId, request);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Id.Should().Be(response.Id);
        }

        [Fact]
        public async Task SaveRotationPlan_Should_ReturnDomainError()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.BadRequest, "RotationPlan.TenantsRequired");

            var result = await CreateClient(handler).SaveRotationPlanAsync(HouseId, new SaveRotationPlanRequest([], [], default));

            result.Error!.ErrorCode.Should().Be("RotationPlan.TenantsRequired");
        }

        [Fact]
        public async Task DeleteRotationPlan_Should_SendDelete()
        {
            var handler = new StubHttpMessageHandler((message, _) =>
            {
                message.Method.Should().Be(HttpMethod.Delete);
                message.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{HouseId}/rotation");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            (await CreateClient(handler).DeleteRotationPlanAsync(HouseId)).IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task SetAndRemoveShiftHelper_Should_UseTheHelperRoute()
        {
            var shiftId = Guid.NewGuid();
            var helperId = Guid.NewGuid();
            var calls = new List<string>();
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{HouseId}/shifts/{shiftId}/helper");
                calls.Add(message.Method.Method);
                if (message.Method == HttpMethod.Put)
                {
                    (await message.Content!.ReadFromJsonAsync<SetShiftHelperRequest>())!.HelperTenantId.Should().Be(helperId);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            var client = CreateClient(handler);

            (await client.SetShiftHelperAsync(HouseId, shiftId, helperId)).IsSuccess.Should().BeTrue();
            (await client.RemoveShiftHelperAsync(HouseId, shiftId)).IsSuccess.Should().BeTrue();

            calls.Should().Equal("PUT", "DELETE");
        }

        [Fact]
        public async Task SetShiftHelper_Should_ReturnDomainError()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.UnprocessableEntity, "Shift.HelperNotAllowed");

            var result = await CreateClient(handler).SetShiftHelperAsync(HouseId, Guid.NewGuid(), Guid.NewGuid());

            result.Error!.ErrorCode.Should().Be("Shift.HelperNotAllowed");
        }

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
