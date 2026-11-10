namespace CoinquiLindo.Application.Reports.Queries
{
    public record GetReportPhotoQuery(Guid HouseId, Guid ShiftId, Guid ReportId, Guid RequesterUserId);
}
