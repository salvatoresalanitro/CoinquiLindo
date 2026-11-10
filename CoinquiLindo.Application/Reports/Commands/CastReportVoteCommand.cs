using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Commands
{
    public record CastReportVoteCommand(Guid HouseId, Guid ShiftId, Guid ReportId, ReportVoteValue Vote, Guid RequesterUserId);
}
