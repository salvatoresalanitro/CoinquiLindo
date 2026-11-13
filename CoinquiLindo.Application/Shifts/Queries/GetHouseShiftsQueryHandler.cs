using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Rotation;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Shifts.Queries
{
    public class GetHouseShiftsQueryHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        IRotationPlanRepository rotationPlanRepository)
    {
        public async Task<List<ShiftResponse>> Handle(GetHouseShiftsQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);
            var requesterTenantId = house.GetRequesterTenant(request.RequesterUserId).Id;

            var shifts = await shiftRepository.GetByHouseIdAsync(request.HouseId, cancellationToken);
            var plan = await rotationPlanRepository.GetByHouseIdAsync(request.HouseId, cancellationToken);
            var tenantNames = house.Tenants.ToDictionary(t => t.Id, t => t.Name);

            List<TenantResponse> HelperCandidates(Shift shift) => shift.CanChooseHelper(requesterTenantId)
                ? plan.GetHelperCandidateIds(shift)
                    .Where(tenantNames.ContainsKey)
                    .Select(id => new TenantResponse(id, tenantNames[id]))
                    .ToList()
                : [];

            return shifts.Select(shift => new ShiftResponse(
                shift.Id,
                shift.HouseId,
                shift.AssignedTenant.Id,
                shift.AssignedTenant.Name,
                shift.AssignedRoom.Id,
                shift.AssignedRoom.Name,
                shift.StartDate,
                shift.EndDate,
                shift.DueDate,
                shift.IsShiftFullyCompleted,
                shift.Checklist
                    .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new ShiftChecklistItemResponse(
                        item.Id,
                        item.Description,
                        item.IsCompleted,
                        item.CompletedAt))
                    .ToList(),
                shift.ToReportResponses(requesterTenantId),
                shift.CanReport(requesterTenantId),
                shift.IsFromRotation,
                shift.HelperTenant?.Id,
                shift.HelperTenant?.Name,
                HelperCandidates(shift),
                shift.HelperTenant is { } helper
                    && (requesterTenantId == shift.AssignedTenant.Id || requesterTenantId == helper.Id)))
                .ToList();
        }
    }
}
