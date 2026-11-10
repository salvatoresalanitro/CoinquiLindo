using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.ValueObjects;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class ReportPhotoFormatTests
    {
        private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

        [Fact]
        public void Detect_Should_RecognizePng_FromSignature()
        {
            var format = ReportPhotoFormat.Detect([.. PngSignature, 0, 0, 0, 13]);

            format.Should().Be(ReportPhotoFormat.Png);
            format.ContentType.Should().Be("image/png");
            format.Extension.Should().Be(".png");
        }

        [Fact]
        public void Detect_Should_RecognizeJpeg_FromSignature()
        {
            var format = ReportPhotoFormat.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 16, 0x4A, 0x46]);

            format.Should().Be(ReportPhotoFormat.Jpeg);
            format.ContentType.Should().Be("image/jpeg");
            format.Extension.Should().Be(".jpg");
        }

        [Fact]
        public void Detect_Should_RejectEmptyContent_AsMissingPhoto()
        {
            var action = () => ReportPhotoFormat.Detect([]);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Report.PhotoRequired");
        }

        [Theory]
        [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67, 0x20, 0x78, 0x6D, 0x6C })]
        [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00 })]
        [InlineData(new byte[] { 0xFF, 0xD8 })]
        [InlineData(new byte[] { 137, 80, 78, 71 })]
        public void Detect_Should_RejectNonJpegOrPngContent(byte[] content)
        {
            var action = () => ReportPhotoFormat.Detect(content);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Report.PhotoInvalid");
        }

        [Fact]
        public void Detect_Should_AcceptExactlyMaxBytes_AndRejectOneByteMore()
        {
            var atLimit = new byte[ReportPhotoFormat.MaxBytes];
            PngSignature.CopyTo(atLimit, 0);
            var overLimit = new byte[ReportPhotoFormat.MaxBytes + 1];
            PngSignature.CopyTo(overLimit, 0);

            ReportPhotoFormat.Detect(atLimit).Should().Be(ReportPhotoFormat.Png);
            var action = () => ReportPhotoFormat.Detect(overLimit);

            ReportPhotoFormat.MaxBytes.Should().Be(5 * 1024 * 1024);
            action.Should().Throw<ReportPhotoTooLargeException>().Which.Code.Should().Be("Report.PhotoTooLarge");
        }

        [Theory]
        [InlineData("0f8fad5bd9cb469fa165708167e1f3e1.png", "image/png")]
        [InlineData("0f8fad5bd9cb469fa165708167e1f3e1.jpg", "image/jpeg")]
        public void FromPhotoKey_Should_ResolveContentType(string photoKey, string contentType)
        {
            ReportPhotoFormat.FromPhotoKey(photoKey).ContentType.Should().Be(contentType);
        }

        [Fact]
        public void CreatePhotoKey_Should_BeRandomOpaqueFileName_WithFormatExtension()
        {
            var first = ReportPhotoFormat.Png.CreatePhotoKey();
            var second = ReportPhotoFormat.Png.CreatePhotoKey();

            first.Should().MatchRegex("^[0-9a-f]{32}\\.png$");
            first.Should().NotBe(second);
        }
    }
}
