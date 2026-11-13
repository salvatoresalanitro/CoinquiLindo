using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.ValueObjects;

namespace CoinquiLindo.Domain.Entities
{
    // Weekly rotation of a house: every period each room moves to the next tenant in the order chosen by the flatmates
    public class RotationPlan
    {
        public const int PeriodLengthDays = 7;

        public Guid Id { get; private set; }
        public Guid HouseId { get; private set; }

        // First day of period 0, a calendar day in the house time zone
        public DateOnly StartDate { get; private set; }

        private List<Guid> _tenantIds = new();
        private List<Guid> _roomIds = new();

        public IReadOnlyList<Guid> TenantIds => _tenantIds.AsReadOnly();
        public IReadOnlyList<Guid> RoomIds => _roomIds.AsReadOnly();

        public RotationPlan(House house, IEnumerable<Guid> tenantIds, IEnumerable<Guid> roomIds, DateOnly startDate)
        {
            ArgumentNullException.ThrowIfNull(house, nameof(house));

            Id = Guid.NewGuid();
            HouseId = house.Id;
            Apply(house, tenantIds, roomIds, startDate);
        }

        public void Update(House house, IEnumerable<Guid> tenantIds, IEnumerable<Guid> roomIds, DateOnly startDate)
        {
            ArgumentNullException.ThrowIfNull(house, nameof(house));
            if (house.Id != HouseId)
            {
                throw new ArgumentException("The plan belongs to another house.", nameof(house));
            }

            Apply(house, tenantIds, roomIds, startDate);
        }

        // Null before the plan starts
        public int? GetPeriodIndex(DateOnly day) =>
            day < StartDate ? null : (day.DayNumber - StartDate.DayNumber) / PeriodLengthDays;

        // Tenant in position i gets the room in position (i + periodIndex) mod N, N being the longer list:
        // positions beyond the shorter list mean a resting tenant or a room without a shift that week
        public RotationPeriod GetPeriod(int periodIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(periodIndex, nameof(periodIndex));

            var cycle = Math.Max(_tenantIds.Count, _roomIds.Count);
            var assignments = new List<RotationAssignment>();
            var resting = new List<Guid>();

            for (var position = 0; position < _tenantIds.Count; position++)
            {
                var roomPosition = (position + periodIndex) % cycle;
                if (roomPosition < _roomIds.Count)
                {
                    assignments.Add(new RotationAssignment(_tenantIds[position], _roomIds[roomPosition]));
                }
                else
                {
                    resting.Add(_tenantIds[position]);
                }
            }

            var unassignedRooms = _roomIds.Except(assignments.Select(a => a.RoomId)).ToList();
            var start = StartDate.AddDays(periodIndex * PeriodLengthDays);

            return new RotationPeriod(periodIndex, start, start.AddDays(PeriodLengthDays - 1), assignments, resting, unassignedRooms);
        }

        // Weeks after the current one whose shifts already exist, so flatmates see them in advance
        public const int MaterializedWeeksAhead = 1;

        // Weeks after which the assignments repeat
        public int CycleLength => Math.Max(_tenantIds.Count, _roomIds.Count);

        // Periods not yet ended on that day and starting within the given weeks: the shifts to materialize
        public IReadOnlyList<RotationPeriod> GetPeriodsToMaterialize(DateOnly today, int weeksAhead = MaterializedWeeksAhead)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(weeksAhead, nameof(weeksAhead));

            var horizon = today.AddDays(weeksAhead * PeriodLengthDays);
            var periods = new List<RotationPeriod>();

            for (var index = GetPeriodIndex(today) ?? 0; StartDate.AddDays(index * PeriodLengthDays) <= horizon; index++)
            {
                periods.Add(GetPeriod(index));
            }

            return periods;
        }

        // Validates everything before changing state, so a failed update leaves the plan untouched
        private void Apply(House house, IEnumerable<Guid> tenantIds, IEnumerable<Guid> roomIds, DateOnly startDate)
        {
            var tenants = tenantIds?.ToList() ?? [];
            var rooms = roomIds?.ToList() ?? [];

            if (tenants.Count == 0)
            {
                throw new DomainValidationException("RotationPlan.TenantsRequired", "A rotation needs at least one tenant.");
            }

            if (rooms.Count == 0)
            {
                throw new DomainValidationException("RotationPlan.RoomsRequired", "A rotation needs at least one room.");
            }

            if (tenants.Distinct().Count() != tenants.Count)
            {
                throw new DomainValidationException("RotationPlan.DuplicateTenant", "A tenant can appear only once in a rotation.");
            }

            if (rooms.Distinct().Count() != rooms.Count)
            {
                throw new DomainValidationException("RotationPlan.DuplicateRoom", "A room can appear only once in a rotation.");
            }

            foreach (var tenantId in tenants.Where(id => house.Tenants.All(t => t.Id != id)))
            {
                throw new NotFoundException(nameof(Tenant), tenantId);
            }

            foreach (var roomId in rooms.Where(id => house.Rooms.All(r => r.Id != id)))
            {
                throw new NotFoundException(nameof(Room), roomId);
            }

            _tenantIds = tenants;
            _roomIds = rooms;
            StartDate = startDate;
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private RotationPlan() { }
#pragma warning restore CS8618
    }
}
