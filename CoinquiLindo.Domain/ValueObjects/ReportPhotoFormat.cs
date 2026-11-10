using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.ValueObjects
{
    public sealed record ReportPhotoFormat
    {
        public const int MaxBytes = 5 * 1024 * 1024;

        public static readonly ReportPhotoFormat Jpeg = new(".jpg", "image/jpeg");
        public static readonly ReportPhotoFormat Png = new(".png", "image/png");

        private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];
        private static ReadOnlySpan<byte> JpegSignature => [0xFF, 0xD8, 0xFF];

        public string Extension { get; }
        public string ContentType { get; }

        private ReportPhotoFormat(string extension, string contentType)
        {
            Extension = extension;
            ContentType = contentType;
        }

        // The format is decided by the bytes only: client file name and Content-Type are untrusted
        public static ReportPhotoFormat Detect(ReadOnlySpan<byte> content)
        {
            if (content.IsEmpty)
            {
                throw new DomainValidationException("Report.PhotoRequired", "A report requires a photo.");
            }

            if (content.Length > MaxBytes)
            {
                throw new ReportPhotoTooLargeException();
            }

            if (content.StartsWith(PngSignature))
            {
                return Png;
            }

            if (content.Length > JpegSignature.Length && content.StartsWith(JpegSignature))
            {
                return Jpeg;
            }

            throw new DomainValidationException("Report.PhotoInvalid", "The report photo must be a JPEG or PNG image.");
        }

        public static ReportPhotoFormat FromPhotoKey(string photoKey) =>
            Path.GetExtension(photoKey) switch
            {
                ".png" => Png,
                ".jpg" => Jpeg,
                _ => throw new ArgumentException($"Unsupported photo key '{photoKey}'.", nameof(photoKey))
            };

        public string CreatePhotoKey() => $"{Guid.NewGuid():N}{Extension}";
    }
}
