using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Commands
{
    public class RemoveReportVoteCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task<ReportResponse> Handle(RemoveReportVoteCommand request, CancellationToken cancellationToken)
        {
            var (_, shift, voter) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);

            shift.RemoveReportVote(request.ReportId, voter.Id);
            await shiftRepository.UpdateAsync(shift, cancellationToken);

            return shift.GetReport(request.ReportId).ToResponse(shift, voter.Id);
        }
    }
}
