using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace CoinquiLindo.Api.Tests
{
    public class InviteEndpointsTests(CoinquiLindoApiFactory factory) : IClassFixture<CoinquiLindoApiFactory>
    {
        [Fact]
        public async Task Invite_Should_LetAnotherUserPreviewAndJoinTheHouse()
        {
            var owner = await factory.CreateAuthenticatedClientAsync(displayName: "Salvo");
            var houseId = await owner.CreateHouseAsync("Casa Milano");
            var inviteCode = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.InviteCode;
            var guest = await factory.CreateAuthenticatedClientAsync(displayName: "Luca");

            var preview = await guest.GetFromJsonAsync<InvitePreviewResponse>($"/api/houses/invites/{FormatForUser(inviteCode)}");
            var join = await guest.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(FormatForUser(inviteCode), null));

            preview!.HouseId.Should().Be(houseId);
            preview.HouseName.Should().Be("Casa Milano");
            preview.IsAlreadyMember.Should().BeFalse();

            join.StatusCode.Should().Be(HttpStatusCode.OK);
            (await join.Content.ReadFromJsonAsync<Guid>()).Should().Be(houseId);

            var guestView = await guest.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}");
            guestView!.Tenants.Select(t => t.Name).Should().BeEquivalentTo("Salvo", "Luca");
            (await guest.GetFromJsonAsync<MeResponse>("/api/me"))!.Houses.Should().ContainSingle(h => h.Id == houseId);
        }

        [Fact]
        public async Task Join_Should_Return409Problem_When_UserIsAlreadyMember()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync();
            var inviteCode = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.InviteCode;

            var response = await owner.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, null));

            (await response.ReadProblemAsync(HttpStatusCode.Conflict)).ErrorCode.Should().Be("House.UserAlreadyMember");
        }

        [Fact]
        public async Task Join_Should_Return409Problem_When_ClaimingTenantWithAccount()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync();
            var house = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var guest = await factory.CreateAuthenticatedClientAsync();

            var response = await guest.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(house.InviteCode, house.Tenants.Single().Id));

            (await response.ReadProblemAsync(HttpStatusCode.Conflict)).ErrorCode.Should().Be("Tenant.AlreadyClaimed");
        }

        [Theory]
        [InlineData("ZZZZ-ZZZZ")]
        [InlineData("malformed")]
        public async Task PreviewAndJoin_Should_Return404Problem_When_CodeIsUnknown(string code)
        {
            var guest = await factory.CreateAuthenticatedClientAsync();

            var preview = await guest.GetAsync($"/api/houses/invites/{code}");
            var join = await guest.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(code, null));

            (await preview.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Invite.NotFound");
            (await join.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Invite.NotFound");
        }

        [Fact]
        public async Task RegenerateInviteCode_Should_InvalidateThePreviousCode()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync();
            var oldCode = (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.InviteCode;

            var regenerate = await owner.PostAsync($"/api/houses/{houseId}/invite-code", null);
            var newCode = (await regenerate.Content.ReadFromJsonAsync<InviteCodeResponse>())!.InviteCode;

            regenerate.StatusCode.Should().Be(HttpStatusCode.OK);
            newCode.Should().NotBe(oldCode);
            (await owner.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!.InviteCode.Should().Be(newCode);

            var guest = await factory.CreateAuthenticatedClientAsync();
            var join = await guest.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(oldCode, null));
            (await join.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Invite.NotFound");
        }

        [Fact]
        public async Task InviteEndpoints_Should_Return401_When_Anonymous()
        {
            var anonymous = factory.CreateClient();

            var preview = await anonymous.GetAsync("/api/houses/invites/ABCDEFGH");
            var join = await anonymous.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest("ABCDEFGH", null));

            (await preview.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            (await join.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
        }

        [Fact]
        public async Task AddTenantByName_Should_NoLongerExist()
        {
            var owner = await factory.CreateAuthenticatedClientAsync();
            var houseId = await owner.CreateHouseAsync();

            var response = await owner.PostAsJsonAsync($"/api/houses/{houseId}/tenants", new { TenantName = "Luca" });

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task InviteEndpoints_Should_Return429Problem_When_UserExceedsRateLimit()
        {
            var limited = factory.WithWebHostBuilder(b =>
            {
                b.UseSetting("RateLimiting:Invites:PermitLimit", "2");
                b.UseSetting("RateLimiting:Invites:WindowSeconds", "60");
            });
            var guest = await limited.CreateAuthenticatedClientAsync();
            var otherGuest = await limited.CreateAuthenticatedClientAsync();

            await guest.GetAsync("/api/houses/invites/ZZZZZZZZ");
            await guest.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest("ZZZZZZZZ", null));
            var limitedResponse = await guest.GetAsync("/api/houses/invites/ZZZZZZZZ");
            var otherUserResponse = await otherGuest.GetAsync("/api/houses/invites/ZZZZZZZZ");

            var problem = await limitedResponse.ReadProblemAsync(HttpStatusCode.TooManyRequests);
            problem.ErrorCode.Should().Be("General.TooManyRequests");
            limitedResponse.Headers.RetryAfter.Should().NotBeNull();

            // Limits are per user: another account is not affected
            otherUserResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        private static string FormatForUser(string code) => $"{code[..4]}-{code[4..]}".ToLowerInvariant();
    }
}
