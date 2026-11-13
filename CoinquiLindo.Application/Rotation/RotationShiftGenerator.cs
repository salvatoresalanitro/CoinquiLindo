using CoinquiLindo.Application.Houses;
using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Rotation
{
    // Turns the periods of a rotation plan into real shifts; shared by the plan handlers and the rotation job
    public class RotationShiftGenerator(
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        // Current calendar day in the house time zone
        public DateOnly Today(House house) => house.Today(timeProvider);

        // Creates the shifts of the periods to materialize that have no shift of the plan yet; returns how many
        public async Task<int> GenerateAsync(House house, RotationPlan plan, Guid? requesterUserId, CancellationToken cancellationToken)
        {
            var periods = plan.GetPeriodsToMaterialize(Today(house));
            if (periods.Count == 0)
            {
                return 0;
            }

            // Any rotation shift of the house counts, also from a deleted plan: a recreated plan must not cover the same week twice
            var existing = await shiftRepository.GetRotationShiftsAsync(house.Id, periods[0].StartDate, cancellationToken);
            var timeZone = house.GetTimeZone();
            var shifts = new List<Shift>();

            foreach (var period in periods)
            {
                // A week already (even partly) covered by the plan is left as it is: no duplicates, no half-rebuilt weeks
                if (existing.Any(s => s.StartDate <= period.EndDate && s.EndDate >= period.StartDate))
                {
                    continue;
                }

                foreach (var assignment in period.Assignments)
                {
                    var tenant = house.Tenants.FirstOrDefault(t => t.Id == assignment.TenantId);
                    var room = house.Rooms.FirstOrDefault(r => r.Id == assignment.RoomId);
                    if (tenant is not null && room is not null)
                    {
                        shifts.Add(new Shift(house.Id, tenant, room, period.StartDate, period.EndDate, timeZone, plan.Id));
                    }
                }
            }

            // False: a concurrent request or job generated the same shifts first
            if (shifts.Count == 0 || !await shiftRepository.TryAddRangeAsync(shifts, cancellationToken))
            {
                return 0;
            }

            foreach (var shift in shifts)
            {
                await notificationPublisher.ShiftAssignedAsync(shift, requesterUserId ?? Guid.Empty, cancellationToken);
            }

            return shifts.Count;
        }

        // Preview of the plan: weeks already generated show their real shifts, the others the plan's assignments
        public Task<RotationPlanResponse> ToResponseAsync(House house, RotationPlan plan, CancellationToken cancellationToken) =>
            plan.ToResponseAsync(house, Today(house), shiftRepository, cancellationToken);

        // Removes the generated shifts not started yet and without activity, so a changed plan can rebuild them
        public async Task RemoveUnstartedAsync(House house, RotationPlan plan, CancellationToken cancellationToken)
        {
            var today = Today(house);
            var shifts = await shiftRepository.GetRotationShiftsAsync(house.Id, today, cancellationToken);

            var removable = shifts
                .Where(s => s.RotationPlanId == plan.Id && s.StartDate > today
                    && s.Reports.Count == 0 && !s.Checklist.Any(item => item.IsCompleted))
                .ToList();

            if (removable.Count > 0)
            {
                await shiftRepository.RemoveRangeAsync(removable, cancellationToken);
            }
        }
    }
}
