using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports
{
    internal static class ReportMapping
    {
        public static ReportResponse ToResponse(this Report report, Shift shift) => new(
            report.Id,
            shift.Id,
            report.Reporter.Id,
            report.Reporter.Name,
            report.Description,
            $"/api/houses/{shift.HouseId}/shifts/{shift.Id}/reports/{report.Id}/photo",
            report.CreatedAt);

        public static List<ReportResponse> ToReportResponses(this Shift shift) =>
            shift.Reports
                .OrderByDescending(report => report.CreatedAt)
                .ThenBy(report => report.Id)
                .Select(report => report.ToResponse(shift))
                .ToList();

        // 404 on the house first, then 403 for non-members, only then shift/report lookups
        public static async Task<(House House, Shift Shift)> GetMemberShiftAsync(
            IHouseRepository houseRepository,
            IShiftRepository shiftRepository,
            Guid houseId,
            Guid shiftId,
            Guid requesterUserId,
            CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(houseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), houseId);

            house.EnsureIsMember(requesterUserId);

            var shift = await shiftRepository.GetByIdAsync(houseId, shiftId, cancellationToken)
                ?? throw new NotFoundException(nameof(Shift), shiftId);

            return (house, shift);
        }

        public static Report GetReport(this Shift shift, Guid reportId) =>
            shift.Reports.FirstOrDefault(report => report.Id == reportId)
                ?? throw new NotFoundException(nameof(Report), reportId);
    }
}
