namespace CoinquiLindo.Infrastructure.Storage
{
    public class ReportPhotoStorageOptions
    {
        public const string SectionName = "ReportPhotos";

        // Absolute directory outside any web root; the host resolves relative values against its content root
        public string RootPath { get; set; } = string.Empty;
    }
}
