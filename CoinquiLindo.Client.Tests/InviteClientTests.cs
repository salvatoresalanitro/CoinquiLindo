using System.Net;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class InviteClientTests
    {
        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") }, NullLogger<CoinquiLindoApiClient>.Instance);

        [Fact]
        public async Task GetInvitePreview_Should_EscapeCodeInPath_And_ReturnPreview()
        {
            var houseId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK,
                $$"""{"houseId":"{{houseId}}","houseName":"Casa","isAlreadyMember":false,"unclaimedTenants":[{"id":"{{tenantId}}","name":"Luca"}]}""");

            var result = await CreateClient(handler).GetInvitePreviewAsync("ab/cd efgh");

            handler.LastRequest!.Method.Should().Be(HttpMethod.Get);
            handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/houses/invites/ab%2Fcd%20efgh");
            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(new InvitePreviewResponse(houseId, "Casa", false, [new TenantResponse(tenantId, "Luca")]));
        }

        [Fact]
        public async Task JoinHouse_Should_PostCodeAndClaimedTenant_And_ReturnHouseId()
        {
            var houseId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, $"\"{houseId}\"");

            var result = await CreateClient(handler).JoinHouseAsync("ABCD-EFGH", tenantId);

            handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
            handler.LastRequest.RequestUri!.AbsolutePath.Should().Be("/api/houses/join");
            var body = await handler.LastRequest.Content!.ReadAsStringAsync();
            body.Should().Contain("ABCD-EFGH").And.Contain(tenantId.ToString());
            result.Value.Should().Be(houseId);
        }

        [Fact]
        public async Task RegenerateInviteCode_Should_ReturnNewCode()
        {
            var houseId = Guid.NewGuid();
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, """{"inviteCode":"ZXCVBNMQ"}""");

            var result = await CreateClient(handler).RegenerateInviteCodeAsync(houseId);

            handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be($"/api/houses/{houseId}/invite-code");
            result.Value.Should().Be(new InviteCodeResponse("ZXCVBNMQ"));
        }

        [Fact]
        public async Task TooManyRequests_Should_MapToLocalizedError()
        {
            var client = CreateClient(StubHttpMessageHandler.ReturningProblem(HttpStatusCode.TooManyRequests, "General.TooManyRequests"));

            var result = await client.JoinHouseAsync("ABCD-EFGH", null);

            result.Error!.Status.Should().Be(429);
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("General.TooManyRequests", 429));
        }

        [Theory]
        [InlineData("ABCDEFGH", "ABCD-EFGH")]
        [InlineData("abcdefgh", "ABCD-EFGH")]
        [InlineData("ABC", "ABC")]
        [InlineData("", "")]
        public void InviteCodes_Format_Should_GroupInTwoBlocks(string code, string expected)
        {
            InviteCodes.Format(code).Should().Be(expected);
        }
    }
}
