using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Commands
{
    public class CastReportVoteCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task<ReportResponse> Handle(CastReportVoteCommand request, CancellationToken cancellationToken)
        {
            var (_, shift, voter) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);

            // Undefined numeric values pass through unchanged and are rejected by the domain
            shift.VoteReport(request.ReportId, voter, (VoteType)(int)request.Vote);
            await shiftRepository.UpdateAsync(shift, cancellationToken);

            return shift.GetReport(request.ReportId).ToResponse(shift, voter.Id);
        }
    }
}
