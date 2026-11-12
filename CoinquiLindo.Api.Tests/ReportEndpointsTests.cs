using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;

namespace CoinquiLindo.Api.Tests
{
    public class ReportEndpointsTests(ReportApiFactory factory) : IClassFixture<ReportApiFactory>
    {
        private const int MaximumPhotoBytes = 5 * 1024 * 1024;

        [Fact]
        public async Task Report_Should_PersistPhotoAndAppearInShiftHistory_WithAuthenticatedAuthor()
        {
            var context = await CreateShiftWithReporterAsync();
            var photo = CreatePng();
            using var form = CreateReportForm("Pavimento ancora sporco", photo);
            form.Add(new StringContent(context.AssigneeTenantId.ToString()), "reporterTenantId");

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
            var created = await response.Content.ReadFromJsonAsync<JsonElement>();
            var reportId = created.GetProperty("id").GetGuid();
            var photoUrl = created.GetProperty("photoUrl").GetString();
            created.GetProperty("shiftId").GetGuid().Should().Be(context.ShiftId);
            created.GetProperty("reporterTenantId").GetGuid().Should().Be(context.ReporterTenantId);
            created.GetProperty("description").GetString().Should().Be("Pavimento ancora sporco");
            photoUrl.Should().Be($"{context.ReportsRoute}/{reportId}/photo");
            response.Headers.Location.Should().NotBeNull();
            response.Headers.Location!.OriginalString.Should().Be($"{context.ReportsRoute}/{reportId}");

            using var detailResponse = await context.Reporter.GetAsync(response.Headers.Location);
            detailResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
            detail.GetProperty("id").GetGuid().Should().Be(reportId);

            var reports = (await context.Reporter.GetFromJsonAsync<JsonElement[]>(context.ReportsRoute))!;
            reports.Should().ContainSingle();
            reports[0].GetProperty("id").GetGuid().Should().Be(reportId);
            reports[0].GetProperty("reporterName").GetString().Should().Be("Segnalatore");

            var shifts = (await context.Assignee.GetFromJsonAsync<List<ShiftResponse>>(
                $"/api/houses/{context.HouseId}/shifts"))!;
            shifts.Single(shift => shift.Id == context.ShiftId).Reports
                .Should().ContainSingle(report => report.Id == reportId && report.PhotoUrl == photoUrl);

            using var missingReport = await context.Reporter.GetAsync($"{context.ReportsRoute}/{Guid.NewGuid()}");
            (await missingReport.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Report.NotFound");

            using var photoResponse = await context.Assignee.GetAsync(photoUrl);
            photoResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            photoResponse.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
            photoResponse.Headers.GetValues("X-Content-Type-Options").Should().Contain("nosniff");
            (await photoResponse.Content.ReadAsByteArrayAsync()).Should().Equal(photo);
            (await response.Content.ReadAsStringAsync()).Should().NotContain(factory.PhotoRootPath);

            using var anonymous = factory.CreateClient();
            using var anonymousResponse = await anonymous.GetAsync(photoUrl);
            (await anonymousResponse.ReadProblemAsync(HttpStatusCode.Unauthorized))
                .ErrorCode.Should().Be("Auth.Unauthorized");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CreateReport_Should_RejectMissingOrEmptyPhoto_EvenWhenPhotoUrlIsSupplied(bool emptyFile)
        {
            var context = await CreateShiftWithReporterAsync();
            var filesBefore = PhotoFiles();
            using var form = CreateReportForm("Pavimento ancora sporco", emptyFile ? [] : null);
            form.Add(new StringContent("https://example.invalid/photo.png"), "photoUrl");

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("Report.PhotoRequired");
            PhotoFiles().Should().BeEquivalentTo(filesBefore);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateReport_Should_RejectMissingDescription_WithoutLeavingPhotoFiles(string? description)
        {
            var context = await CreateShiftWithReporterAsync();
            var filesBefore = PhotoFiles();
            using var form = CreateReportForm(description, CreatePng());

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest))
                .ErrorCode.Should().Be("Report.DescriptionRequired");
            PhotoFiles().Should().BeEquivalentTo(filesBefore);
        }

        [Theory]
        [InlineData("image/png", "photo.png")]
        [InlineData("image/jpeg", "photo.jpg")]
        [InlineData("image/svg+xml", "photo.svg")]
        public async Task CreateReport_Should_RejectNonImageContent_RegardlessOfFileNameAndContentType(
            string contentType, string fileName)
        {
            var context = await CreateShiftWithReporterAsync();
            var filesBefore = PhotoFiles();
            using var form = CreateReportForm("Pavimento sporco", Encoding.UTF8.GetBytes("not an image"),
                contentType, fileName);

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            (await response.ReadProblemAsync(HttpStatusCode.BadRequest)).ErrorCode.Should().Be("Report.PhotoInvalid");
            PhotoFiles().Should().BeEquivalentTo(filesBefore);
        }

        [Fact]
        public async Task CreateReport_Should_AcceptPhotoAtExactlyFiveMiB()
        {
            var context = await CreateShiftWithReporterAsync();
            var photo = CreatePng(MaximumPhotoBytes);
            photo.Should().HaveCount(MaximumPhotoBytes);
            using var form = CreateReportForm("Pavimento sporco", photo);

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task CreateReport_Should_RejectPhotoOneByteAboveFiveMiB_WithoutLeavingPhotoFiles()
        {
            var context = await CreateShiftWithReporterAsync();
            var filesBefore = PhotoFiles();
            using var form = CreateReportForm("Pavimento sporco", CreatePng(MaximumPhotoBytes + 1));

            using var response = await context.Reporter.PostAsync(context.ReportsRoute, form);

            (await response.ReadProblemAsync(HttpStatusCode.RequestEntityTooLarge))
                .ErrorCode.Should().Be("Report.PhotoTooLarge");
            PhotoFiles().Should().BeEquivalentTo(filesBefore);
        }

        [Fact]
        public async Task CreateReport_Should_RejectSelfReport_WithoutLeavingPhotoFiles()
        {
            var context = await CreateShiftWithReporterAsync();
            var filesBefore = PhotoFiles();
            using var form = CreateReportForm("Il mio turno", CreatePng());

            using var response = await context.Assignee.PostAsync(context.ReportsRoute, form);

            (await response.ReadProblemAsync(HttpStatusCode.UnprocessableEntity))
                .ErrorCode.Should().Be("Shift.SelfReportNotAllowed");
            PhotoFiles().Should().BeEquivalentTo(filesBefore);
        }

        [Fact]
        public async Task ReportEndpoints_Should_RequireHouseMembership()
        {
            var context = await CreateShiftWithReporterAsync();
            using var outsider = await factory.CreateAuthenticatedClientAsync();
            using var form = CreateReportForm("Pavimento sporco", CreatePng());

            using var create = await outsider.PostAsync(context.ReportsRoute, form);
            using var list = await outsider.GetAsync(context.ReportsRoute);
            using var photo = await outsider.GetAsync($"{context.ReportsRoute}/{Guid.NewGuid()}/photo");

            foreach (var response in new[] { create, list, photo })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Forbidden)).ErrorCode.Should().Be("House.AccessDenied");
            }
        }

        [Fact]
        public async Task ReportEndpoints_Should_RejectAnonymousRequests()
        {
            using var anonymous = factory.CreateClient();
            var route = $"/api/houses/{Guid.NewGuid()}/shifts/{Guid.NewGuid()}/reports";
            using var form = CreateReportForm("Pavimento sporco", CreatePng());

            using var create = await anonymous.PostAsync(route, form);
            using var list = await anonymous.GetAsync(route);
            using var photo = await anonymous.GetAsync($"{route}/{Guid.NewGuid()}/photo");

            foreach (var response in new[] { create, list, photo })
            {
                (await response.ReadProblemAsync(HttpStatusCode.Unauthorized)).ErrorCode.Should().Be("Auth.Unauthorized");
            }
        }

        [Fact]
        public async Task CreateReport_Should_NotAcceptShiftFromAnotherHouse()
        {
            var context = await CreateShiftWithReporterAsync();
            var otherHouseId = await context.Reporter.CreateHouseAsync("Altra casa");
            using var form = CreateReportForm("Pavimento sporco", CreatePng());

            using var response = await context.Reporter.PostAsync(
                $"/api/houses/{otherHouseId}/shifts/{context.ShiftId}/reports", form);

            (await response.ReadProblemAsync(HttpStatusCode.NotFound)).ErrorCode.Should().Be("Shift.NotFound");
        }

        private async Task<ReportContext> CreateShiftWithReporterAsync()
        {
            var assignee = await factory.CreateAuthenticatedClientAsync(displayName: "Assegnatario");
            var houseId = await assignee.CreateHouseAsync("Casa report");
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

            var reporter = await factory.CreateAuthenticatedClientAsync(displayName: "Segnalatore");
            using var joinResponse = await reporter.PostAsJsonAsync(
                "/api/houses/join", new JoinHouseRequest(house.InviteCode, null));
            joinResponse.EnsureSuccessStatusCode();
            house = (await reporter.GetFromJsonAsync<HouseResponse>($"/api/houses/{houseId}"))!;
            var reporterTenantId = house.Tenants.Single(tenant => tenant.Id != assigneeTenantId).Id;

            return new ReportContext(assignee, reporter, houseId, shiftId, assigneeTenantId, reporterTenantId);
        }

        private string[] PhotoFiles() => Directory.Exists(factory.PhotoRootPath)
            ? Directory.GetFiles(factory.PhotoRootPath, "*", SearchOption.AllDirectories)
            : [];

        private static MultipartFormDataContent CreateReportForm(
            string? description, byte[]? photo, string contentType = "image/png", string fileName = "camera.png")
        {
            var form = new MultipartFormDataContent();
            if (description is not null)
            {
                form.Add(new StringContent(description), "description");
            }

            if (photo is not null)
            {
                var file = new ByteArrayContent(photo);
                file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
                form.Add(file, "photo", fileName);
            }

            return form;
        }

        private static byte[] CreatePng(int? totalLength = null)
        {
            using var image = new MemoryStream();
            image.Write([137, 80, 78, 71, 13, 10, 26, 10]);
            var header = new byte[13];
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 1);
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 1);
            header[8] = 8;
            header[9] = 6;
            WritePngChunk(image, "IHDR", header);

            using var compressed = new MemoryStream();
            using (var zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                zlib.Write([0, 255, 255, 255, 255]);
            }

            WritePngChunk(image, "IDAT", compressed.ToArray());
            if (totalLength.HasValue)
            {
                // A valid ancillary chunk pads the image without corrupting its encoded pixels.
                var text = new byte[checked(totalLength.Value - (int)image.Length - 24)];
                Array.Fill(text, (byte)'x');
                text[0] = (byte)'n';
                text[1] = 0;
                WritePngChunk(image, "tEXt", text);
            }

            WritePngChunk(image, "IEND", []);
            return image.ToArray();
        }

        private static void WritePngChunk(Stream destination, string type, byte[] data)
        {
            Span<byte> value = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(value, (uint)data.Length);
            destination.Write(value);
            var typeBytes = Encoding.ASCII.GetBytes(type);
            destination.Write(typeBytes);
            destination.Write(data);
            var crc = uint.MaxValue;
            foreach (var part in new[] { typeBytes, data })
            {
                foreach (var item in part)
                {
                    crc ^= item;
                    for (var bit = 0; bit < 8; bit++)
                    {
                        crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0u);
                    }
                }
            }

            BinaryPrimitives.WriteUInt32BigEndian(value, ~crc);
            destination.Write(value);
        }

        private sealed record ReportContext(
            HttpClient Assignee, HttpClient Reporter, Guid HouseId, Guid ShiftId,
            Guid AssigneeTenantId, Guid ReporterTenantId)
        {
            public string ReportsRoute => $"/api/houses/{HouseId}/shifts/{ShiftId}/reports";
        }
    }

    public class ReportApiFactory : CoinquiLindoApiFactory
    {
        public string PhotoRootPath { get; } = Path.Combine(
            Path.GetTempPath(), "CoinquiLindo.ReportTests", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("ReportPhotos:RootPath", PhotoRootPath);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(PhotoRootPath))
            {
                Directory.Delete(PhotoRootPath, recursive: true);
            }
        }
    }
}
