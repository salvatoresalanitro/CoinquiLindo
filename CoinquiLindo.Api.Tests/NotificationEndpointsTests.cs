using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CoinquiLindo.Application.Notifications.Commands;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace CoinquiLindo.Api.Tests
{
    public class NotificationEndpointsTests(ReportApiFactory factory) : IClassFixture<ReportApiFactory>
    {
        private const string NotificationsRoute = "/api/me/notifications";
        private static readonly byte[] JpegPhoto = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70];

        [Fact]
        public async Task Notifications_Should_FollowShiftReportAndVoteLifecycle()
        {
            var house = await CreateHouseWithMembersAsync();

            var shiftId = await AssignShiftAsync(house, DateTime.UtcNow.Date.AddDays(7));
            var assigneeNotifications = await GetNotificationsAsync(house.Assignee);
            assigneeNotifications.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Type = NotificationKind.ShiftAssigned, HouseId = house.HouseId, ShiftId = (Guid?)shiftId, ReadAt = (DateTime?)null
            });
            (await GetNotificationsAsync(house.Owner)).Should().BeEmpty("the requester is not notified of its own action");

            var reportId = await CreateReportAsync(house, shiftId);
            var reportForAssignee = (await GetNotificationsAsync(house.Assignee)).First();
            reportForAssignee.Should().BeEquivalentTo(new
            {
                Type = NotificationKind.ReportCreated, Title = "Nuova segnalazione sul tuo turno", ReportId = (Guid?)reportId
            });
            (await GetNotificationsAsync(house.Voter)).Should().ContainSingle(n => n.Type == NotificationKind.ReportCreated);

            await VoteAsync(house.Voter, house, shiftId, reportId);
            var voteNotification = (await GetNotificationsAsync(house.Owner)).Should().ContainSingle().Subject;
            voteNotification.Type.Should().Be(NotificationKind.ReportVoted);
            voteNotification.Body.Should().Contain("1 d'accordo");

            (await house.Assignee.GetFromJsonAsync<UnreadNotificationsResponse>($"{NotificationsRoute}/unread-count"))!
                .Count.Should().Be(2);

            using var foreignRead = await house.Owner.PutAsync($"{NotificationsRoute}/{reportForAssignee.Id}/read", null);
            (await foreignRead.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Notification.NotFound");

            using var read = await house.Assignee.PutAsync($"{NotificationsRoute}/{reportForAssignee.Id}/read", null);
            read.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await GetNotificationsAsync(house.Assignee, unreadOnly: true))
                .Should().ContainSingle().Which.Type.Should().Be(NotificationKind.ShiftAssigned);

            using var readAll = await house.Assignee.PutAsync($"{NotificationsRoute}/read-all", null);
            readAll.StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await house.Assignee.GetFromJsonAsync<UnreadNotificationsResponse>($"{NotificationsRoute}/unread-count"))!
                .Count.Should().Be(0);
            (await GetNotificationsAsync(house.Assignee)).Should().HaveCount(2).And.OnlyContain(n => n.ReadAt != null);
        }

        [Fact]
        public async Task ShiftDueSoonJob_Should_NotifyAssigneeOnce()
        {
            var house = await CreateHouseWithMembersAsync();
            var shiftId = await AssignShiftAsync(house, DateTime.UtcNow.AddHours(12));

            await RunDueSoonJobAsync();
            await RunDueSoonJobAsync();

            (await GetNotificationsAsync(house.Assignee))
                .Should().ContainSingle(n => n.Type == NotificationKind.ShiftDueSoon)
                .Which.ShiftId.Should().Be(shiftId);
        }

        [Fact]
        public async Task NotificationEndpoints_Should_RejectAnonymousRequests()
        {
            using var anonymous = factory.CreateClient();

            using var list = await anonymous.GetAsync(NotificationsRoute);
            using var count = await anonymous.GetAsync($"{NotificationsRoute}/unread-count");
            using var read = await anonymous.PutAsync($"{NotificationsRoute}/{Guid.NewGuid()}/read", null);
            using var readAll = await anonymous.PutAsync($"{NotificationsRoute}/read-all", null);

            foreach (var response in new[] { list, count, read, readAll })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            }
        }

        private async Task RunDueSoonJobAsync()
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync<int>(new CreateShiftDueSoonNotificationsCommand(TimeSpan.FromHours(24)));
        }

        private static async Task<List<NotificationResponse>> GetNotificationsAsync(HttpClient client, bool unreadOnly = false) =>
            (await client.GetFromJsonAsync<List<NotificationResponse>>($"{NotificationsRoute}?unreadOnly={unreadOnly}"))!;

        private static async Task<Guid> AssignShiftAsync(HouseContext house, DateTime endDate)
        {
            using var response = await house.Owner.PostAsJsonAsync($"/api/houses/{house.HouseId}/shifts",
                new AssignShiftRequest(house.AssigneeTenantId, house.RoomId, endDate.AddDays(-7), endDate));
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<Guid>();
        }

        private static async Task<Guid> CreateReportAsync(HouseContext house, Guid shiftId)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("Pavimento ancora sporco"), ReportForm.DescriptionField);
            var photo = new ByteArrayContent(JpegPhoto);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(photo, ReportForm.PhotoField, "camera.jpg");
            using var response = await house.Owner.PostAsync($"/api/houses/{house.HouseId}/shifts/{shiftId}/reports", form);
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            return (await response.Content.ReadFromJsonAsync<ReportResponse>())!.Id;
        }

        private static async Task VoteAsync(HttpClient voter, HouseContext house, Guid shiftId, Guid reportId)
        {
            using var response = await voter.PutAsJsonAsync(
                $"/api/houses/{house.HouseId}/shifts/{shiftId}/reports/{reportId}/vote", new CastReportVoteRequest(ReportVoteValue.Up));
            response.EnsureSuccessStatusCode();
        }

        private async Task<HouseContext> CreateHouseWithMembersAsync()
        {
            var owner = await factory.CreateAuthenticatedClientAsync(displayName: "Proprietario");
            var houseId = await owner.CreateHouseAsync("Casa notifiche");
            var house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            using var roomResponse = await owner.PostAsJsonAsync(
                $"/api/houses/{houseId}/rooms", new AddRoomRequest("Bagno", ["Lavare il pavimento"]));
            roomResponse.EnsureSuccessStatusCode();
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();

            var assignee = await JoinAsync("Assegnatario", house.InviteCode);
            var voter = await JoinAsync("Votante", house.InviteCode);
            house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var assigneeTenantId = house.Tenants.Single(t => t.Name == "Assegnatario").Id;

            return new HouseContext(owner, assignee, voter, houseId, roomId, assigneeTenantId);
        }

        private async Task<HttpClient> JoinAsync(string displayName, string inviteCode)
        {
            var client = await factory.CreateAuthenticatedClientAsync(displayName: displayName);
            using var joinResponse = await client.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, null));
            joinResponse.EnsureSuccessStatusCode();
            return client;
        }

        private sealed record HouseContext(
            HttpClient Owner, HttpClient Assignee, HttpClient Voter, Guid HouseId, Guid RoomId, Guid AssigneeTenantId);
    }
}
