using System.Net;
using System.Net.Http.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Client.Tests
{
    public class ReportClientTests
    {
        private static readonly byte[] JpegPhoto = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

        [Fact]
        public async Task CreateShiftReport_Should_PostMultipartWithDescriptionAndPhoto()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var expected = new ReportResponse(Guid.NewGuid(), shiftId, Guid.NewGuid(), "Salvo", "Pavimento sporco",
                $"/api/houses/{houseId}/shifts/{shiftId}/reports/x/photo", DateTime.UtcNow, 0, 0, 0, null, true);
            var handler = new StubHttpMessageHandler(async (request, ct) =>
            {
                request.Method.Should().Be(HttpMethod.Post);
                request.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts/{shiftId}/reports");
                var form = request.Content.Should().BeOfType<MultipartFormDataContent>().Subject;
                var parts = form.ToList();
                var description = parts.Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == ReportForm.DescriptionField);
                (await description.ReadAsStringAsync(ct)).Should().Be("Pavimento sporco");
                var photo = parts.Single(p => p.Headers.ContentDisposition!.Name!.Trim('"') == ReportForm.PhotoField);
                photo.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
                photo.Headers.ContentDisposition!.FileName.Should().NotBeNullOrWhiteSpace();
                (await photo.ReadAsByteArrayAsync(ct)).Should().Equal(JpegPhoto);
                return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(expected) };
            });

            var result = await CreateClient(handler)
                .CreateShiftReportAsync(houseId, shiftId, "Pavimento sporco", JpegPhoto, "image/jpeg");

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task CreateShiftReport_Should_MapProblemToItalianMessage()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.RequestEntityTooLarge, "Report.PhotoTooLarge");

            var result = await CreateClient(handler)
                .CreateShiftReportAsync(Guid.NewGuid(), Guid.NewGuid(), "Pavimento sporco", JpegPhoto, "image/jpeg");

            result.IsSuccess.Should().BeFalse();
            result.Error!.ErrorCode.Should().Be("Report.PhotoTooLarge");
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("Report.PhotoTooLarge", 413));
        }

        [Fact]
        public async Task GetShiftReports_Should_ReadReportsFromShiftRoute()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var report = new ReportResponse(Guid.NewGuid(), shiftId, Guid.NewGuid(), "Salvo", "Pavimento sporco",
                "/photo", DateTime.UtcNow, 2, 1, 1, ReportVoteValue.Down, true);
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts/{shiftId}/reports");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new List<ReportResponse> { report })
                });
            });

            var result = await CreateClient(handler).GetShiftReportsAsync(houseId, shiftId);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().ContainSingle().Which.Should().BeEquivalentTo(report);
        }

        [Fact]
        public async Task GetReportPhoto_Should_ReturnBytesAndContentType()
        {
            const string photoUrl = "/api/houses/h/shifts/s/reports/r/photo";
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.RequestUri!.PathAndQuery.Should().Be(photoUrl);
                var content = new ByteArrayContent(JpegPhoto);
                content.Headers.ContentType = new("image/jpeg");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
            });

            var result = await CreateClient(handler).GetReportPhotoAsync(photoUrl);

            result.IsSuccess.Should().BeTrue();
            result.Value!.Content.Should().Equal(JpegPhoto);
            result.Value.ContentType.Should().Be("image/jpeg");
        }

        [Theory]
        [InlineData("https://attacker.example/photo")]
        [InlineData("//attacker.example/photo")]
        [InlineData("/not-api/photo")]
        public async Task GetReportPhoto_Should_RefuseUrlsOutsideTheApi_WithoutSendingRequest(string photoUrl)
        {
            var handler = StubHttpMessageHandler.Returning(HttpStatusCode.OK, "unused");

            var result = await CreateClient(handler).GetReportPhotoAsync(photoUrl);

            result.IsSuccess.Should().BeFalse();
            handler.LastRequest.Should().BeNull();
        }

        [Fact]
        public async Task CastReportVote_Should_PutVoteAsStringAndReadUpdatedReport()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var expected = new ReportResponse(reportId, shiftId, Guid.NewGuid(), "Salvo", "Pavimento sporco", "/photo",
                DateTime.UtcNow, 1, 0, 1, ReportVoteValue.Up, true);
            var handler = new StubHttpMessageHandler(async (request, ct) =>
            {
                request.Method.Should().Be(HttpMethod.Put);
                request.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts/{shiftId}/reports/{reportId}/vote");
                (await request.Content!.ReadAsStringAsync(ct)).Should().Contain("\"vote\":\"Up\"");
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) };
            });

            var result = await CreateClient(handler).CastReportVoteAsync(houseId, shiftId, reportId, ReportVoteValue.Up);

            result.IsSuccess.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(expected);
        }

        [Fact]
        public async Task RemoveReportVote_Should_DeleteVoteAndReadUpdatedReport()
        {
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var expected = new ReportResponse(reportId, shiftId, Guid.NewGuid(), "Salvo", "Pavimento sporco", "/photo",
                DateTime.UtcNow, 0, 0, 0, null, true);
            var handler = new StubHttpMessageHandler((request, _) =>
            {
                request.Method.Should().Be(HttpMethod.Delete);
                request.RequestUri!.PathAndQuery.Should().Be($"/api/houses/{houseId}/shifts/{shiftId}/reports/{reportId}/vote");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(expected) });
            });

            var result = await CreateClient(handler).RemoveReportVoteAsync(houseId, shiftId, reportId);

            result.IsSuccess.Should().BeTrue();
            result.Value!.MyVote.Should().BeNull();
        }

        [Fact]
        public async Task CastReportVote_Should_MapConflictToItalianMessage()
        {
            var handler = StubHttpMessageHandler.ReturningProblem(HttpStatusCode.Conflict, "ReportVote.Conflict");

            var result = await CreateClient(handler)
                .CastReportVoteAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ReportVoteValue.Down);

            result.IsSuccess.Should().BeFalse();
            result.Error!.Status.Should().Be(409);
            result.Error.UserMessage.Should().Be(ErrorMessageProvider.GetMessage("ReportVote.Conflict", 409));
        }

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
