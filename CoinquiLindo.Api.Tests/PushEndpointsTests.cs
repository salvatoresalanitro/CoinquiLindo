using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoinquiLindo.Api.Tests
{
    public class PushEndpointsTests(PushApiFactory factory) : IClassFixture<PushApiFactory>
    {
        private const string DevicesRoute = "/api/me/devices";

        private RecordingPushSender Sender => factory.Sender;

        [Fact]
        public async Task RegisteredDevice_Should_ReceivePushForNewNotifications()
        {
            var house = await CreateHouseAsync();
            var token = NewToken();
            await RegisterAsync(house.Assignee, token);

            await AssignShiftAsync(house, house.AssigneeTenantId);

            var push = await Sender.WaitForAsync(m => m.Tokens.Contains(token));
            push.Title.Should().Be("Nuovo turno assegnato");
            push.Data["houseId"].Should().Be(house.HouseId.ToString());
            push.Data["type"].Should().Be(nameof(NotificationKind.ShiftAssigned));
            push.Data["unreadCount"].Should().Be("1");
        }

        [Fact]
        public async Task Token_Should_MoveToTheLastAccountThatRegisteredIt()
        {
            var house = await CreateHouseAsync();
            var shared = NewToken();
            var marker = NewToken();
            await RegisterAsync(house.Assignee, shared);
            await RegisterAsync(house.Other, shared);
            await RegisterAsync(house.Other, marker);

            await AssignShiftAsync(house, house.AssigneeTenantId);
            await AssignShiftAsync(house, house.OtherTenantId);

            // Pushes are dispatched in order: once the second one is out, the first was already processed
            await Sender.WaitForAsync(m => m.Tokens.Contains(marker));
            Sender.Sent.Where(m => m.Tokens.Contains(shared)).Should().ContainSingle()
                .Which.Tokens.Should().Contain(marker, "the shared token now belongs to the second account");
        }

        [Fact]
        public async Task UnregisteredAndRejectedTokens_Should_NoLongerReceivePushes()
        {
            var house = await CreateHouseAsync();
            var removed = NewToken();
            var dead = NewToken();
            var alive = NewToken();
            Sender.InvalidTokens[dead] = true;
            await RegisterAsync(house.Assignee, removed);
            await RegisterAsync(house.Assignee, dead);
            await RegisterAsync(house.Assignee, alive);
            using (var unregister = await house.Assignee.PostAsJsonAsync($"{DevicesRoute}/unregister", new UnregisterPushDeviceRequest(removed)))
            {
                unregister.StatusCode.Should().Be(HttpStatusCode.NoContent);
            }

            await AssignShiftAsync(house, house.AssigneeTenantId);
            var first = await Sender.WaitForAsync(m => m.Tokens.Contains(alive));
            first.Tokens.Should().BeEquivalentTo([dead, alive]);

            await AssignShiftAsync(house, house.AssigneeTenantId);
            await Sender.WaitForAsync(m => m.Tokens.Contains(alive) && !m.Tokens.Contains(dead));
        }

        [Theory]
        [InlineData("""{"token":" ","platform":"Android"}""", "PushDevice.TokenRequired")]
        [InlineData("""{"token":"abc","platform":"Symbian"}""", "General.BadRequest")]
        public async Task RegisterDevice_Should_RejectInvalidInput(string json, string errorCode)
        {
            using var client = await factory.CreateAuthenticatedClientAsync();
            using var body = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PutAsync(DevicesRoute, body);

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be(errorCode);
        }

        [Fact]
        public async Task DeviceEndpoints_Should_RejectAnonymousRequests()
        {
            using var anonymous = factory.CreateClient();

            using var register = await anonymous.PutAsJsonAsync(DevicesRoute, new RegisterPushDeviceRequest("t", PushPlatformValue.Android));
            using var unregister = await anonymous.PostAsJsonAsync($"{DevicesRoute}/unregister", new UnregisterPushDeviceRequest("t"));

            foreach (var response in new[] { register, unregister })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            }
        }

        private static string NewToken() => $"fcm-{Guid.NewGuid():N}";

        private static async Task RegisterAsync(HttpClient client, string token)
        {
            using var response = await client.PutAsJsonAsync(DevicesRoute, new RegisterPushDeviceRequest(token, PushPlatformValue.Android));
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }

        private static async Task AssignShiftAsync(HouseContext house, Guid tenantId)
        {
            var end = DateTime.UtcNow.Date.AddDays(7);
            using var response = await house.Owner.PostAsJsonAsync($"/api/houses/{house.HouseId}/shifts",
                new AssignShiftRequest(tenantId, house.RoomId, end.AddDays(-7), end));
            response.EnsureSuccessStatusCode();
        }

        private async Task<HouseContext> CreateHouseAsync()
        {
            var owner = await factory.CreateAuthenticatedClientAsync(displayName: "Proprietario");
            var houseId = await owner.CreateHouseAsync("Casa push");
            var house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            using var roomResponse = await owner.PostAsJsonAsync($"/api/houses/{houseId}/rooms", new AddRoomRequest("Bagno", ["Lavare"]));
            roomResponse.EnsureSuccessStatusCode();
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();

            var assignee = await JoinAsync("Assegnatario", house.InviteCode);
            var other = await JoinAsync("Altro", house.InviteCode);
            house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;

            return new HouseContext(owner, assignee, other, houseId, roomId,
                house.Tenants.Single(t => t.Name == "Assegnatario").Id,
                house.Tenants.Single(t => t.Name == "Altro").Id);
        }

        private async Task<HttpClient> JoinAsync(string displayName, string inviteCode)
        {
            var client = await factory.CreateAuthenticatedClientAsync(displayName: displayName);
            using var joinResponse = await client.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, null));
            joinResponse.EnsureSuccessStatusCode();
            return client;
        }

        private sealed record HouseContext(
            HttpClient Owner, HttpClient Assignee, HttpClient Other, Guid HouseId, Guid RoomId,
            Guid AssigneeTenantId, Guid OtherTenantId);
    }

    public class PushApiFactory : CoinquiLindoApiFactory
    {
        public RecordingPushSender Sender => (RecordingPushSender)Services.GetRequiredService<IPushSender>();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            // Concrete registration (no factory lambda): Wolverine builds handlers without service location
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPushSender>();
                services.AddSingleton<IPushSender, RecordingPushSender>();
            });
        }
    }

    public sealed class RecordingPushSender : IPushSender
    {
        private readonly ConcurrentQueue<PushMessage> _sent = new();

        public ConcurrentDictionary<string, bool> InvalidTokens { get; } = new();

        public IReadOnlyCollection<PushMessage> Sent => _sent.ToArray();

        public Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken)
        {
            _sent.Enqueue(message);
            var invalid = message.Tokens.Where(InvalidTokens.ContainsKey).ToList();
            return Task.FromResult(new PushSendResult(message.Tokens.Count - invalid.Count, invalid));
        }

        public async Task<PushMessage> WaitForAsync(Func<PushMessage, bool> predicate)
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (DateTime.UtcNow < deadline)
            {
                var match = _sent.FirstOrDefault(predicate);
                if (match is not null)
                {
                    return match;
                }

                await Task.Delay(25);
            }

            throw new TimeoutException("Expected push was not sent within 10 seconds.");
        }
    }
}
