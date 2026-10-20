using System.Net;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class CoinquiLindoApiClientTests
    {
        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, NullLogger<CoinquiLindoApiClient>.Instance);

        [Fact]
        public async Task CreateHouse_Should_ReturnId_When_ApiSucceeds()
        {
            var id = Guid.NewGuid();
            var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK, $"\"{id}\""));

            var result = await client.CreateHouseAsync("Casa");

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(id);
            result.Error.Should().BeNull();
        }

        [Fact]
        public async Task GetHouse_Should_ReturnHouse_When_ApiSucceeds()
        {
            var id = Guid.NewGuid();
            var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.OK,
                $$"""{"id":"{{id}}","name":"Casa","tenants":[],"rooms":[]}"""));

            var result = await client.GetHouseAsync(id);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new HouseResponse(id, "Casa", [], []));
        }

        [Fact]
        public async Task AddRoom_Should_MapProblemDetails_ToLocalizedError()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.Conflict, "House.RoomNameAlreadyExists", "00-abc-01");
            var client = CreateClient(handler);

            var result = await client.AddRoomAsync(Guid.NewGuid(), "Cucina", []);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Status.Should().Be(409);
            result.Error.ErrorCode.Should().Be("House.RoomNameAlreadyExists");
            result.Error.TraceId.Should().Be("00-abc-01");
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("House.RoomNameAlreadyExists", 409));
            result.Error.UserMessage.Should().NotContain("developer-oriented");
        }

        [Fact]
        public async Task AddTenant_Should_ReturnHouseNotFoundError_When_Api404()
        {
            var client = CreateClient(StubHttpMessageHandler.ReturningProblem(HttpStatusCode.NotFound, "House.NotFound"));

            var result = await client.AddTenantAsync(Guid.NewGuid(), "Salvo");

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be("House.NotFound");
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("House.NotFound", 404));
        }

        [Fact]
        public async Task UnknownErrorCode_Should_FallBackToStatusMessage()
        {
            var client = CreateClient(StubHttpMessageHandler.ReturningProblem(HttpStatusCode.NotFound, "Future.UnknownCode"));

            var result = await client.GetHouseAsync(Guid.NewGuid());

            result.Error!.ErrorCode.Should().Be("Future.UnknownCode");
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage(null, 404));
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", "application/json")]
        [InlineData("<html>Bad Gateway</html>", "text/html")]
        [InlineData("{ broken json", "application/problem+json")]
        public async Task UnreadableErrorBody_Should_StillProduceError(string? body, string? mediaType)
        {
            var client = CreateClient(StubHttpMessageHandler.Returning(HttpStatusCode.BadGateway, body, mediaType ?? "application/json"));

            var result = await client.CreateHouseAsync("Casa");

            result.IsSuccess.Should().BeFalse();
            result.Error!.Status.Should().Be(502);
            result.Error.ErrorCode.Should().BeNull();
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage(null, 502));
        }

        [Fact]
        public async Task NetworkFailure_Should_ReturnNetworkUnavailable()
        {
            var client = CreateClient(StubHttpMessageHandler.Throwing(new HttpRequestException("Connection refused")));

            var result = await client.CreateHouseAsync("Casa");

            result.IsSuccess.Should().BeFalse();
            result.Error!.Status.Should().BeNull();
            result.Error.ErrorCode.Should().Be(ApiError.NetworkUnavailableCode);
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage(ApiError.NetworkUnavailableCode, null));
        }

        [Fact]
        public async Task Timeout_Should_ReturnNetworkUnavailable()
        {
            // HttpClient surfaces timeouts as TaskCanceledException wrapping a TimeoutException
            var client = CreateClient(StubHttpMessageHandler.Throwing(new TaskCanceledException("Timeout", new TimeoutException())));

            var result = await client.AddTenantAsync(Guid.NewGuid(), "Salvo");

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be(ApiError.NetworkUnavailableCode);
        }

        [Fact]
        public async Task CallerCancellation_Should_Propagate()
        {
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();
            var client = CreateClient(new StubHttpMessageHandler((_, ct) => Task.FromCanceled<HttpResponseMessage>(ct)));

            var action = () => client.GetHouseAsync(Guid.NewGuid(), cts.Token);

            await action.Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
