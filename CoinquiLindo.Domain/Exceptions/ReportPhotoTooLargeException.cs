using CoinquiLindo.Domain.ValueObjects;

namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class ReportPhotoTooLargeException()
        : PayloadTooLargeException("Report.PhotoTooLarge",
            $"The report photo cannot exceed {ReportPhotoFormat.MaxBytes} bytes.");
}
