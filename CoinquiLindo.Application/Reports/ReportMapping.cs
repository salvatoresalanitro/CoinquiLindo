using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Reports
{
    internal static class ReportMapping
    {
        // Vote state is per requester: the same report reads differently for each tenant
        public static ReportResponse ToResponse(this Report report, Shift shift, Guid requesterTenantId) => new(
            report.Id,
            shift.Id,
            report.Reporter.Id,
            report.Reporter.Name,
            report.Description,
            $"/api/houses/{shift.HouseId}/shifts/{shift.Id}/reports/{report.Id}/photo",
            report.CreatedAt,
            report.UpvoteCount,
            report.DownvoteCount,
            report.Score,
            report.VoteOf(requesterTenantId) is { } vote ? (ReportVoteValue)(int)vote : null,
            shift.CanVoteOnReport(report, requesterTenantId));

        public static List<ReportResponse> ToReportResponses(this Shift shift, Guid requesterTenantId) =>
            shift.Reports
                .OrderByDescending(report => report.CreatedAt)
                .ThenBy(report => report.Id)
                .Select(report => report.ToResponse(shift, requesterTenantId))
                .ToList();

        // Callers run EnsureIsMember first, so the tenant always exists
        public static Tenant GetRequesterTenant(this House house, Guid requesterUserId) =>
            house.Tenants.First(tenant => tenant.UserId == requesterUserId);

        // 404 on the house first, then 403 for non-members, only then shift/report lookups
        public static async Task<(House House, Shift Shift, Tenant Requester)> GetMemberShiftAsync(
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

            return (house, shift, house.GetRequesterTenant(requesterUserId));
        }
    }
}
