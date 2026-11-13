using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class CleaningTaskClientTests
    {
        private static readonly Guid HouseId = Guid.NewGuid();
        private static readonly Guid RoomId = Guid.NewGuid();
        private static readonly Guid TaskId = Guid.NewGuid();
        private static string TasksRoute => $"/api/houses/{HouseId}/rooms/{RoomId}/tasks";

        [Fact]
        public async Task AddCleaningTask_Should_PostTheDescription_AndReturnTheTaskId()
        {
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.Method.Should().Be(HttpMethod.Post);
                message.RequestUri!.PathAndQuery.Should().Be(TasksRoute);
                (await message.Content!.ReadFromJsonAsync<AddCleaningTaskRequest>())!.Description.Should().Be("Pulire i vetri");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(TaskId) };
            });

            var result = await CreateClient(handler).AddCleaningTaskAsync(HouseId, RoomId, "Pulire i vetri");

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(TaskId);
        }

        [Fact]
        public async Task UpdateAndRemoveCleaningTask_Should_UseTheTaskRoute()
        {
            var calls = new List<string>();
            var handler = new StubHttpMessageHandler(async (message, _) =>
            {
                message.RequestUri!.PathAndQuery.Should().Be($"{TasksRoute}/{TaskId}");
                calls.Add(message.Method.Method);
                if (message.Method == HttpMethod.Put)
                {
                    (await message.Content!.ReadFromJsonAsync<UpdateCleaningTaskRequest>())!.Description.Should().Be("Lavare e asciugare");
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });
            var client = CreateClient(handler);

            (await client.UpdateCleaningTaskAsync(HouseId, RoomId, TaskId, "Lavare e asciugare")).IsSuccess.Should().BeTrue();
            (await client.RemoveCleaningTaskAsync(HouseId, RoomId, TaskId)).IsSuccess.Should().BeTrue();

            calls.Should().Equal("PUT", "DELETE");
        }

        [Theory]
        [InlineData(HttpStatusCode.Conflict, "Room.CleaningTaskAlreadyExists")]
        [InlineData(HttpStatusCode.NotFound, "CleaningTask.NotFound")]
        public async Task CleaningTaskErrors_Should_BeMappedToLocalizedMessages(HttpStatusCode status, string errorCode)
        {
            var handler = StubHttpMessageHandler.ReturningProblem(status, errorCode);
            var client = CreateClient(handler);

            var add = await client.AddCleaningTaskAsync(HouseId, RoomId, "Lavare i piatti");
            var update = await client.UpdateCleaningTaskAsync(HouseId, RoomId, TaskId, "Lavare i piatti");

            add.Error!.ErrorCode.Should().Be(errorCode);
            update.Error!.ErrorCode.Should().Be(errorCode);
            add.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage(errorCode, (int)status));
        }

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
