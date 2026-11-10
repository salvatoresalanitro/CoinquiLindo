using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Queries
{
    public class GetShiftReportQueryHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task<ReportResponse> Handle(GetShiftReportQuery request, CancellationToken cancellationToken)
        {
            var (_, shift) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);

            return shift.GetReport(request.ReportId).ToResponse(shift);
        }
    }
}
