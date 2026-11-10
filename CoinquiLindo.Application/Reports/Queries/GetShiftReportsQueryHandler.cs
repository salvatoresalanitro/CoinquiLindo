using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports.Queries
{
    public class GetShiftReportsQueryHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task<List<ReportResponse>> Handle(GetShiftReportsQuery request, CancellationToken cancellationToken)
        {
            var (_, shift, requester) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);

            return shift.ToReportResponses(requester.Id);
        }
    }
}
