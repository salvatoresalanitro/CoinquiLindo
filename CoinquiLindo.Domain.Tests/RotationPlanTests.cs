using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.ValueObjects;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class RotationPlanTests
    {
        private static readonly DateOnly Monday = new(2026, 11, 2);

        private static (House House, List<Guid> Tenants, List<Guid> Rooms) NewHouse(int tenants, int rooms)
        {
            var house = new House("Casa");
            for (var i = 1; i <= tenants; i++)
            {
                house.AddTenant(new Tenant($"Utente {i}"));
            }

            for (var i = 1; i <= rooms; i++)
            {
                house.AddRoom(new Room($"Stanza {i}"));
            }

            return (house, house.Tenants.Select(t => t.Id).ToList(), house.Rooms.Select(r => r.Id).ToList());
        }

        [Fact]
        public void GetPeriod_Should_ShiftRoomsByOnePositionEveryWeek()
        {
            var (house, t, r) = NewHouse(3, 3);
            var plan = new RotationPlan(house, t, r, Monday);

            plan.GetPeriod(0).Assignments.Should().Equal(
                new RotationAssignment(t[0], r[0]), new RotationAssignment(t[1], r[1]), new RotationAssignment(t[2], r[2]));
            plan.GetPeriod(1).Assignments.Should().Equal(
                new RotationAssignment(t[0], r[1]), new RotationAssignment(t[1], r[2]), new RotationAssignment(t[2], r[0]));
            plan.GetPeriod(2).Assignments.Should().Equal(
                new RotationAssignment(t[0], r[2]), new RotationAssignment(t[1], r[0]), new RotationAssignment(t[2], r[1]));
            plan.GetPeriod(3).Assignments.Should().Equal(plan.GetPeriod(0).Assignments);
            plan.GetPeriod(1).RestingTenantIds.Should().BeEmpty();
            plan.GetPeriod(1).UnassignedRoomIds.Should().BeEmpty();
        }

        [Fact]
        public void GetPeriod_Should_SetWeekDates()
        {
            var (house, t, r) = NewHouse(2, 2);
            var plan = new RotationPlan(house, t, r, Monday);

            var period = plan.GetPeriod(2);

            period.Index.Should().Be(2);
            period.StartDate.Should().Be(new DateOnly(2026, 11, 16));
            period.EndDate.Should().Be(new DateOnly(2026, 11, 22));
        }

        [Fact]
        public void GetPeriod_Should_LetTenantsRestInTurn_When_TenantsAreMoreThanRooms()
        {
            var (house, t, r) = NewHouse(4, 3);
            var plan = new RotationPlan(house, t, r, Monday);

            plan.GetPeriod(0).RestingTenantIds.Should().Equal(t[3]);
            plan.GetPeriod(1).RestingTenantIds.Should().Equal(t[2]);
            plan.GetPeriod(2).RestingTenantIds.Should().Equal(t[1]);
            plan.GetPeriod(3).RestingTenantIds.Should().Equal(t[0]);
            plan.GetPeriod(1).Assignments.Should().Equal(
                new RotationAssignment(t[0], r[1]), new RotationAssignment(t[1], r[2]), new RotationAssignment(t[3], r[0]));
            Enumerable.Range(0, 4).Should().OnlyContain(i => plan.GetPeriod(i).UnassignedRoomIds.Count == 0);
        }

        [Fact]
        public void GetPeriod_Should_LeaveRoomsWithoutShiftInTurn_When_RoomsAreMoreThanTenants()
        {
            var (house, t, r) = NewHouse(2, 3);
            var plan = new RotationPlan(house, t, r, Monday);

            plan.GetPeriod(0).UnassignedRoomIds.Should().Equal(r[2]);
            plan.GetPeriod(1).UnassignedRoomIds.Should().Equal(r[0]);
            plan.GetPeriod(1).Assignments.Should().Equal(
                new RotationAssignment(t[0], r[1]), new RotationAssignment(t[1], r[2]));
            plan.GetPeriod(2).Assignments.Should().Equal(new RotationAssignment(t[0], r[2]), new RotationAssignment(t[1], r[0]));
            plan.GetPeriod(2).RestingTenantIds.Should().BeEmpty();
        }

        [Fact]
        public void Plan_Should_KeepTheOrderChosenByTheFlatmates()
        {
            var (house, t, r) = NewHouse(3, 3);

            var plan = new RotationPlan(house, [t[2], t[0]], [r[1], r[0]], Monday);

            plan.HouseId.Should().Be(house.Id);
            plan.TenantIds.Should().Equal(t[2], t[0]);
            plan.RoomIds.Should().Equal(r[1], r[0]);
            plan.GetPeriod(0).Assignments.Should().Equal(new RotationAssignment(t[2], r[1]), new RotationAssignment(t[0], r[0]));
        }

        [Theory]
        [InlineData("2026-11-01", null)]
        [InlineData("2026-11-02", 0)]
        [InlineData("2026-11-08", 0)]
        [InlineData("2026-11-09", 1)]
        [InlineData("2026-12-07", 5)]
        public void GetPeriodIndex_Should_CountWeeksFromStartDate(string day, int? expected)
        {
            var (house, t, r) = NewHouse(2, 2);
            var plan = new RotationPlan(house, t, r, Monday);

            plan.GetPeriodIndex(DateOnly.Parse(day)).Should().Be(expected);
        }

        [Fact]
        public void Plan_Should_ThrowValidation_When_ListsAreEmptyOrHaveDuplicates()
        {
            var (house, t, r) = NewHouse(2, 2);

            Code(() => new RotationPlan(house, [], r, Monday)).Should().Be("RotationPlan.TenantsRequired");
            Code(() => new RotationPlan(house, t, [], Monday)).Should().Be("RotationPlan.RoomsRequired");
            Code(() => new RotationPlan(house, [t[0], t[0]], r, Monday)).Should().Be("RotationPlan.DuplicateTenant");
            Code(() => new RotationPlan(house, t, [r[1], r[1]], Monday)).Should().Be("RotationPlan.DuplicateRoom");
        }

        [Fact]
        public void Plan_Should_ThrowNotFound_When_TenantOrRoomIsNotInTheHouse()
        {
            var (house, t, r) = NewHouse(2, 2);

            Code(() => new RotationPlan(house, [t[0], Guid.NewGuid()], r, Monday)).Should().Be("Tenant.NotFound");
            Code(() => new RotationPlan(house, t, [Guid.NewGuid()], Monday)).Should().Be("Room.NotFound");
        }

        [Fact]
        public void Update_Should_ReplaceOrderAndStartDate_WithTheSameValidation()
        {
            var (house, t, r) = NewHouse(3, 2);
            var plan = new RotationPlan(house, t, r, Monday);
            var id = plan.Id;

            plan.Update(house, [t[1], t[0]], [r[1]], Monday.AddDays(7));

            plan.Id.Should().Be(id);
            plan.TenantIds.Should().Equal(t[1], t[0]);
            plan.RoomIds.Should().Equal(r[1]);
            plan.StartDate.Should().Be(Monday.AddDays(7));
            Code(() => plan.Update(house, [], r, Monday)).Should().Be("RotationPlan.TenantsRequired");
            plan.TenantIds.Should().Equal(t[1], t[0]);
        }

        [Theory]
        // Current week plus the next one
        [InlineData("2026-11-04", new[] { 0, 1 })]
        // Last day of week 0: it has not ended yet
        [InlineData("2026-11-08", new[] { 0, 1 })]
        [InlineData("2026-11-09", new[] { 1, 2 })]
        // Plan not started: week 0 falls within the next 7 days
        [InlineData("2026-10-28", new[] { 0 })]
        [InlineData("2026-10-26", new[] { 0 })]
        [InlineData("2026-10-25", new int[0])]
        public void GetPeriodsToMaterialize_Should_ReturnCurrentAndNextWeek(string today, int[] expected)
        {
            var (house, t, r) = NewHouse(3, 3);
            var plan = new RotationPlan(house, t, r, Monday);

            var periods = plan.GetPeriodsToMaterialize(DateOnly.Parse(today));

            periods.Select(p => p.Index).Should().Equal(expected);
            periods.Should().OnlyContain(p => p.Assignments.Count == 3);
        }

        [Fact]
        public void GetPeriodsToMaterialize_Should_HonourWeeksAhead_AndRejectNegativeValues()
        {
            var (house, t, r) = NewHouse(2, 2);
            var plan = new RotationPlan(house, t, r, Monday);
            var today = new DateOnly(2026, 11, 10);

            plan.GetPeriodsToMaterialize(today, 0).Select(p => p.Index).Should().Equal(1);
            plan.GetPeriodsToMaterialize(today, 2).Select(p => p.Index).Should().Equal(1, 2, 3);
            plan.GetPeriodsToMaterialize(today, 2)[2].StartDate.Should().Be(new DateOnly(2026, 11, 23));

            var negative = () => plan.GetPeriodsToMaterialize(today, -1);
            negative.Should().Throw<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(4, 3, 4)]
        [InlineData(2, 3, 3)]
        [InlineData(1, 1, 1)]
        public void CycleLength_Should_BeTheLongerList(int tenants, int rooms, int expected)
        {
            var (house, t, r) = NewHouse(tenants, rooms);

            new RotationPlan(house, t, r, Monday).CycleLength.Should().Be(expected);
        }

        private static string Code(Action action) => action.Should().Throw<DomainException>().Which.Code;
    }

    public class ShiftHelperTests
    {
        private readonly Tenant _assignee = new("Lucia");
        private readonly Tenant _resting = new("Marco");

        private Shift NewShift(Guid? rotationPlanId) => new(Guid.NewGuid(), _assignee, new Room("Bagno"),
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc, rotationPlanId);

        [Fact]
        public void SetHelper_Should_StoreHelper_When_ChosenByTheAssigneeOfARotationShift()
        {
            var shift = NewShift(Guid.NewGuid());

            shift.SetHelper(_assignee.Id, _resting);

            shift.IsFromRotation.Should().BeTrue();
            shift.HelperTenant.Should().BeSameAs(_resting);
            shift.CanChooseHelper(_assignee.Id).Should().BeTrue();
            shift.CanChooseHelper(_resting.Id).Should().BeFalse();
        }

        [Fact]
        public void SetHelper_Should_Throw_When_ShiftIsManual_OrRequesterIsNotTheAssignee_OrHelperIsTheAssignee()
        {
            var manual = NewShift(null);
            var rotation = NewShift(Guid.NewGuid());

            manual.CanChooseHelper(_assignee.Id).Should().BeFalse();
            var actions = new Action[]
            {
                () => manual.SetHelper(_assignee.Id, _resting),
                () => rotation.SetHelper(_resting.Id, _resting),
                () => rotation.SetHelper(_assignee.Id, _assignee)
            };

            foreach (var action in actions)
            {
                action.Should().Throw<ShiftHelperNotAllowedException>().Which.Code.Should().Be("Shift.HelperNotAllowed");
            }

            rotation.HelperTenant.Should().BeNull();
        }

        [Fact]
        public void RemoveHelper_Should_BeAllowedToAssigneeAndHelper_Only()
        {
            var shift = NewShift(Guid.NewGuid());
            shift.SetHelper(_assignee.Id, _resting);

            var stranger = () => shift.RemoveHelper(Guid.NewGuid());
            stranger.Should().Throw<ShiftHelperNotAllowedException>();
            shift.HelperTenant.Should().NotBeNull();

            shift.RemoveHelper(_resting.Id);
            shift.HelperTenant.Should().BeNull();

            shift.SetHelper(_assignee.Id, _resting);
            shift.RemoveHelper(_assignee.Id);
            shift.HelperTenant.Should().BeNull();

            // Nothing to remove: no-op
            shift.RemoveHelper(_assignee.Id);
        }
    }
}
