namespace CoinquiLindo.Application.Reports.Commands
{
    public record RemoveReportVoteCommand(Guid HouseId, Guid ShiftId, Guid ReportId, Guid RequesterUserId);
}
