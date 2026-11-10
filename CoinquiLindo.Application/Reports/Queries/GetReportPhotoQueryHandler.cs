using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Domain.ValueObjects;

namespace CoinquiLindo.Application.Reports.Queries
{
    public class GetReportPhotoQueryHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        IReportPhotoStorage photoStorage)
    {
        public async Task<ReportPhotoContent> Handle(GetReportPhotoQuery request, CancellationToken cancellationToken)
        {
            var (_, shift) = await ReportMapping.GetMemberShiftAsync(houseRepository, shiftRepository,
                request.HouseId, request.ShiftId, request.RequesterUserId, cancellationToken);
            var report = shift.GetReport(request.ReportId);

            var content = await photoStorage.OpenReadAsync(report.PhotoKey, cancellationToken)
                ?? throw new NotFoundException("ReportPhoto", report.Id);

            return new ReportPhotoContent(content, ReportPhotoFormat.FromPhotoKey(report.PhotoKey).ContentType);
        }
    }
}
