namespace CoinquiLindo.Application.Reports.Queries
{
    public record GetShiftReportQuery(Guid HouseId, Guid ShiftId, Guid ReportId, Guid RequesterUserId);
}
