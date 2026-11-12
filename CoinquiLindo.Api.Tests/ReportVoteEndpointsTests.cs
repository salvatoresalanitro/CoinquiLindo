using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;

namespace CoinquiLindo.Api.Tests
{
    public class ReportVoteEndpointsTests(ReportApiFactory factory) : IClassFixture<ReportApiFactory>
    {
        // Only the magic bytes are inspected by the server
        private static readonly byte[] JpegPhoto = [0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 74, 70, 73, 70];

        [Fact]
        public async Task Vote_Should_BeCastChangedAndRemoved_AndAppearInShiftHistory()
        {
            var context = await CreateReportAsync();

            using var up = await context.Voter.PutAsJsonAsync(context.VoteRoute, new CastReportVoteRequest(ReportVoteValue.Up));
            up.StatusCode.Should().Be(HttpStatusCode.OK);
            (await up.Content.ReadFromJsonAsync<ReportResponse>()).Should().BeEquivalentTo(new
            {
                Id = context.ReportId, UpvoteCount = 1, DownvoteCount = 0, Score = 1,
                MyVote = (ReportVoteValue?)ReportVoteValue.Up, CanVote = true
            });

            using var down = await context.Voter.PutAsJsonAsync(context.VoteRoute, new CastReportVoteRequest(ReportVoteValue.Down));
            (await down.Content.ReadFromJsonAsync<ReportResponse>()).Should().BeEquivalentTo(new
            {
                UpvoteCount = 0, DownvoteCount = 1, Score = -1, MyVote = (ReportVoteValue?)ReportVoteValue.Down
            });

            var assigneeView = (await context.Assignee.GetFromJsonAsync<List<ShiftResponse>>(context.ShiftsRoute))!
                .Single(shift => shift.Id == context.ShiftId);
            assigneeView.CanReport.Should().BeFalse();
            assigneeView.Reports.Single().Should().BeEquivalentTo(new
            {
                Score = -1, MyVote = (ReportVoteValue?)null, CanVote = false
            });
            var reporterView = (await context.Reporter.GetFromJsonAsync<List<ShiftResponse>>(context.ShiftsRoute))!
                .Single(shift => shift.Id == context.ShiftId);
            reporterView.CanReport.Should().BeTrue();
            reporterView.Reports.Single().CanVote.Should().BeFalse();

            using var removed = await context.Voter.DeleteAsync(context.VoteRoute);
            removed.StatusCode.Should().Be(HttpStatusCode.OK);
            (await removed.Content.ReadFromJsonAsync<ReportResponse>()).Should().BeEquivalentTo(new
            {
                Score = 0, MyVote = (ReportVoteValue?)null
            });

            using var removedAgain = await context.Voter.DeleteAsync(context.VoteRoute);
            removedAgain.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Vote_Should_SerializeVoteAsString()
        {
            var context = await CreateReportAsync();
            using var body = new StringContent("""{"vote":"Up"}""", Encoding.UTF8, "application/json");

            using var response = await context.Voter.PutAsync(context.VoteRoute, body);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            (await response.Content.ReadAsStringAsync()).Should().Contain("\"myVote\":\"Up\"");
        }

        [Theory]
        [InlineData("""{"vote":"Sideways"}""", "General.BadRequest")]
        [InlineData("""{"vote":7}""", "ReportVote.InvalidType")]
        public async Task Vote_Should_RejectInvalidVoteValues(string json, string errorCode)
        {
            var context = await CreateReportAsync();
            using var body = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await context.Voter.PutAsync(context.VoteRoute, body);

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be(errorCode);
        }

        [Fact]
        public async Task Vote_Should_RejectReporterAndAssignee()
        {
            var context = await CreateReportAsync();

            using var byReporter = await context.Reporter.PutAsJsonAsync(context.VoteRoute, new CastReportVoteRequest(ReportVoteValue.Up));
            using var byAssignee = await context.Assignee.PutAsJsonAsync(context.VoteRoute, new CastReportVoteRequest(ReportVoteValue.Down));

            foreach (var response in new[] { byReporter, byAssignee })
            {
                (await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity)).ErrorCode.Should().Be("ReportVote.NotAllowed");
            }
        }

        [Fact]
        public async Task Vote_Should_ReturnReportNotFound_ForUnknownReport()
        {
            var context = await CreateReportAsync();

            using var response = await context.Voter.PutAsJsonAsync(
                $"{context.ReportsRoute}/{Guid.NewGuid()}/vote", new CastReportVoteRequest(ReportVoteValue.Up));

            (await response.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Report.NotFound");
        }

        [Fact]
        public async Task VoteEndpoints_Should_RequireHouseMembership()
        {
            var context = await CreateReportAsync();
            using var outsider = await factory.CreateAuthenticatedClientAsync();

            using var cast = await outsider.PutAsJsonAsync(context.VoteRoute, new CastReportVoteRequest(ReportVoteValue.Up));
            using var remove = await outsider.DeleteAsync(context.VoteRoute);

            foreach (var response in new[] { cast, remove })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            }
        }

        [Fact]
        public async Task VoteEndpoints_Should_RejectAnonymousRequests()
        {
            using var anonymous = factory.CreateClient();
            var route = $"/api/houses/{Guid.NewGuid()}/shifts/{Guid.NewGuid()}/reports/{Guid.NewGuid()}/vote";

            using var cast = await anonymous.PutAsJsonAsync(route, new CastReportVoteRequest(ReportVoteValue.Up));
            using var remove = await anonymous.DeleteAsync(route);

            foreach (var response in new[] { cast, remove })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            }
        }

        private async Task<VoteContext> CreateReportAsync()
        {
            var assignee = await factory.CreateAuthenticatedClientAsync(displayName: "Assegnatario");
            var houseId = await assignee.CreateHouseAsync("Casa voti");
            var house = (await assignee.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var assigneeTenantId = house.Tenants.Single().Id;
            using var roomResponse = await assignee.PostAsJsonAsync(
                $"/api/houses/{houseId}/rooms", new AddRoomRequest("Bagno", ["Lavare il pavimento"]));
            roomResponse.EnsureSuccessStatusCode();
            var roomId = await roomResponse.Content.ReadFromJsonAsync<Guid>();
            using var shiftResponse = await assignee.PostAsJsonAsync(
                $"/api/houses/{houseId}/shifts",
                new AssignShiftRequest(assigneeTenantId, roomId,
                    DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-7), DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1)));
            shiftResponse.EnsureSuccessStatusCode();
            var shiftId = await shiftResponse.Content.ReadFromJsonAsync<Guid>();

            var reporter = await JoinAsync("Segnalatore", house.InviteCode);
            var voter = await JoinAsync("Votante", house.InviteCode);

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("Pavimento ancora sporco"), ReportForm.DescriptionField);
            var photo = new ByteArrayContent(JpegPhoto);
            photo.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(photo, ReportForm.PhotoField, "camera.jpg");
            using var reportResponse = await reporter.PostAsync($"/api/houses/{houseId}/shifts/{shiftId}/reports", form);
            reportResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var report = (await reportResponse.Content.ReadFromJsonAsync<ReportResponse>())!;

            return new VoteContext(assignee, reporter, voter, houseId, shiftId, report.Id);
        }

        private async Task<HttpClient> JoinAsync(string displayName, string inviteCode)
        {
            var client = await factory.CreateAuthenticatedClientAsync(displayName: displayName);
            using var joinResponse = await client.PostAsJsonAsync("/api/houses/join", new JoinHouseRequest(inviteCode, null));
            joinResponse.EnsureSuccessStatusCode();
            return client;
        }

        private sealed record VoteContext(
            HttpClient Assignee, HttpClient Reporter, HttpClient Voter, Guid HouseId, Guid ShiftId, Guid ReportId)
        {
            public string ShiftsRoute => $"/api/houses/{HouseId}/shifts";
            public string ReportsRoute => $"/api/houses/{HouseId}/shifts/{ShiftId}/reports";
            public string VoteRoute => $"{ReportsRoute}/{ReportId}/vote";
        }
    }
}
