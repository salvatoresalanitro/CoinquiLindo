namespace CoinquiLindo.Shared.Contracts
{
    public record ReportResponse(
        Guid Id,
        Guid ShiftId,
        Guid ReporterTenantId,
        string ReporterName,
        string Description,
        string PhotoUrl,
        DateTime CreatedAt);

    // multipart/form-data contract of POST /api/houses/{houseId}/shifts/{shiftId}/reports
    public static class ReportForm
    {
        public const string DescriptionField = "description";
        public const string PhotoField = "photo";
        public const int MaxPhotoBytes = 5 * 1024 * 1024;
    }
}
