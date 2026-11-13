using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Domain.ValueObjects;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Rotation
{
    internal static class RotationMapping
    {
        public const int MaxPreviewWeeks = 8;

        // Current calendar day in the house time zone: weeks and deadlines are interpreted there (ADR-018)
        public static DateOnly Today(this House house, TimeProvider timeProvider) =>
            DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), house.GetTimeZone()).DateTime);

        public static async Task<RotationPlanResponse> ToResponseAsync(
            this RotationPlan plan, House house, DateOnly today, IShiftRepository shiftRepository, CancellationToken cancellationToken)
        {
            var firstWeek = plan.GetPeriod(plan.GetPeriodIndex(today) ?? 0);
            var generated = await shiftRepository.GetRotationShiftsAsync(house.Id, firstWeek.StartDate, cancellationToken);

            return plan.ToResponse(house, today, generated);
        }

        // Weeks that already have rotation shifts show those shifts: a changed plan applies only to the weeks not generated yet
        public static RotationPlanResponse ToResponse(this RotationPlan plan, House house, DateOnly today, IReadOnlyCollection<Shift> generated)
        {
            var tenants = house.Tenants.ToDictionary(t => t.Id);
            var rooms = house.Rooms.ToDictionary(r => r.Id);
            string TenantName(Guid id) => tenants.TryGetValue(id, out var tenant) ? tenant.Name : string.Empty;
            string RoomName(Guid id) => rooms.TryGetValue(id, out var room) ? room.Name : string.Empty;

            var firstWeek = plan.GetPeriodIndex(today) ?? 0;
            var weeks = Enumerable.Range(firstWeek, Math.Min(plan.CycleLength, MaxPreviewWeeks))
                .Select(plan.GetPeriod)
                .Select(period => period.WithGeneratedShifts(plan, generated))
                .Select(period => new RotationWeekResponse(
                    period.Index,
                    period.StartDate,
                    period.EndDate,
                    period.Assignments
                        .Select(a => new RotationAssignmentResponse(a.TenantId, TenantName(a.TenantId), a.RoomId, RoomName(a.RoomId)))
                        .ToList(),
                    period.RestingTenantIds.Select(id => new TenantResponse(id, TenantName(id))).ToList(),
                    period.UnassignedRoomIds.Select(id => new RotationRoomResponse(id, RoomName(id))).ToList()))
                .ToList();

            return new RotationPlanResponse(
                plan.Id,
                plan.StartDate,
                plan.TenantIds.Select(id => new TenantResponse(id, TenantName(id))).ToList(),
                plan.RoomIds.Select(id => new RotationRoomResponse(id, RoomName(id))).ToList(),
                weeks);
        }

        private static RotationPeriod WithGeneratedShifts(this RotationPeriod period, RotationPlan plan, IReadOnlyCollection<Shift> generated)
        {
            var shifts = generated.Where(s => s.StartDate <= period.EndDate && s.EndDate >= period.StartDate).ToList();
            if (shifts.Count == 0)
            {
                return period;
            }

            // Ordered as the plan, so the preview keeps a stable order
            var positions = plan.TenantIds.Select((id, index) => (id, index)).ToDictionary(x => x.id, x => x.index);
            var assignments = shifts
                .Select(s => new RotationAssignment(s.AssignedTenant.Id, s.AssignedRoom.Id))
                .OrderBy(a => positions.GetValueOrDefault(a.TenantId, int.MaxValue))
                .ToList();

            return period with
            {
                Assignments = assignments,
                RestingTenantIds = plan.TenantIds.Where(id => assignments.All(a => a.TenantId != id)).ToList(),
                UnassignedRoomIds = plan.RoomIds.Where(id => assignments.All(a => a.RoomId != id)).ToList()
            };
        }

        // Tenants resting in the week of the shift according to the current plan, assignee excluded.
        // Empty for manual shifts and for shifts of a plan that has been deleted and recreated.
        public static IReadOnlyList<Guid> GetHelperCandidateIds(this RotationPlan? plan, Shift shift)
        {
            if (plan is null || shift.RotationPlanId != plan.Id || plan.GetPeriodIndex(shift.StartDate) is not { } index)
            {
                return [];
            }

            return plan.GetPeriod(index).RestingTenantIds.Where(id => id != shift.AssignedTenant.Id).ToList();
        }
    }
}
