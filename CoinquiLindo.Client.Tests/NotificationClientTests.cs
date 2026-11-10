using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class NotificationClientTests
    {
        [Fact]
        public async Task GetNotifications_Should_PassFiltersAndReadKindAsString()
        {
            var notificationId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Get);
                request.RequestUri!.PathAndQuery.Should().Be("/api/me/notifications?unreadOnly=true&take=20");
                var json = $$"""
                    [{"id":"{{notificationId}}","type":"ReportVoted","title":"Nuovi voti","body":"1 d'accordo",
                      "houseId":"{{Guid.Empty}}","shiftId":null,"reportId":null,"createdAt":"2026-11-10T09:00:00Z","readAt":null}]
                    """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
                });
            });

            var result = await CreateClient(handler).GetNotificationsAsync(unreadOnly: true, take: 20);

            result.IsSuccess.Should().BeTrue();
            var notification = result.Value.Should().ContainSingle().Subject;
            notification.Id.Should().Be(notificationId);
            notification.Type.Should().Be(NotificationKind.ReportVoted);
        }

        [Fact]
        public async Task GetUnreadNotificationsCount_Should_ReadCount()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.RequestUri!.PathAndQuery.Should().Be("/api/me/notifications/unread-count");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new UnreadNotificationsResponse(4))
                });
            });

            var result = await CreateClient(handler).GetUnreadNotificationsCountAsync();

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().Be(4);
        }

        [Fact]
        public async Task MarkNotificationAsRead_Should_PutReadRoute()
        {
            var notificationId = Guid.NewGuid();
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Put);
                request.RequestUri!.PathAndQuery.Should().Be($"/api/me/notifications/{notificationId}/read");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            var result = await CreateClient(handler).MarkNotificationAsReadAsync(notificationId);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task MarkAllNotificationsAsRead_Should_PutReadAllRoute()
        {
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Put);
                request.RequestUri!.PathAndQuery.Should().Be("/api/me/notifications/read-all");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
            });

            var result = await CreateClient(handler).MarkAllNotificationsAsReadAsync();

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task MarkNotificationAsRead_Should_MapNotFoundToItalianMessage()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.NotFound, "Notification.NotFound");

            var result = await CreateClient(handler).MarkNotificationAsReadAsync(Guid.NewGuid());

            result.IsSuccess.Should().BeFalse();
            result.Error!.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("Notification.NotFound", 404));
        }

        [Fact]
        public async Task RegisterPushDevice_Should_PutTokenAndPlatformAsString()
        {
            var handler = new StubHttpMessageHandler(async (request, ct) =>
            {
                request.Method.Should().Be(HttpMethod.Put);
                request.RequestUri!.PathAndQuery.Should().Be("/api/me/devices");
                var body = await request.Content!.ReadAsStringAsync(ct);
                body.Should().Contain("\"token\":\"fcm-token\"").And.Contain("\"platform\":\"Android\"");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            var result = await CreateClient(handler).RegisterPushDeviceAsync("fcm-token", PushPlatformValue.Android);

            result.IsSuccess.Should().BeTrue();
        }

        [Fact]
        public async Task UnregisterPushDevice_Should_PostTokenInBody()
        {
            var handler = new StubHttpMessageHandler(async (request, ct) =>
            {
                request.Method.Should().Be(HttpMethod.Post);
                request.RequestUri!.PathAndQuery.Should().Be("/api/me/devices/unregister");
                (await request.Content!.ReadAsStringAsync(ct)).Should().Contain("\"token\":\"fcm-token\"");
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            });

            var result = await CreateClient(handler).UnregisterPushDeviceAsync("fcm-token");

            result.IsSuccess.Should().BeTrue();
        }

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
