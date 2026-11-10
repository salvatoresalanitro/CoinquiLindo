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
                $"/api/houses/{houseId}/shifts/{shiftId}/reports/x/photo", DateTime.UtcNow);
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
                "/photo", DateTime.UtcNow);
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

        private static CoinquiLindoApiClient CreateClient(HttpMessageHandler handler) =>
            new(new HttpClient(handler) { BaseAddress = new Uri("http://localhost") },
                NullLogger<CoinquiLindoApiClient>.Instance);
    }
}
