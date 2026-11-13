using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Rotation;
using CoinquiLindo.Application.Rotation.Commands;
using CoinquiLindo.Application.Rotation.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    internal sealed record RotationScenario(House House, List<User> Users, List<Tenant> Tenants, List<Room> Rooms)
    {
        public static readonly DateOnly Monday = new(2026, 11, 2);

        // Every tenant has an account; users[0] is the first member
        public static RotationScenario Create(int members, int rooms, string timeZoneId = "Europe/Rome")
        {
            var (house, first) = TestData.HouseWithMember("Casa rotazione", timeZoneId);
            var users = new List<User> { first };
            for (var i = 2; i <= members; i++)
            {
                var user = TestData.NewUser($"Utente{i}");
                house.AddMember(user);
                users.Add(user);
            }

            for (var i = 1; i <= rooms; i++)
            {
                var room = new Room($"Stanza {i}");
                room.AddCleaningTask(new CleaningTask($"Pulire stanza {i}"));
                house.AddRoom(room);
            }

            var tenants = users.Select(u => house.Tenants.Single(t => t.UserId == u.Id)).ToList();
            return new RotationScenario(house, users, tenants, house.Rooms.ToList());
        }

        public RotationPlan Plan(DateOnly? startDate = null) =>
            new(House, Tenants.Select(t => t.Id), Rooms.Select(r => r.Id), startDate ?? Monday);

        public Shift RotationShift(RotationPlan plan, int tenant, int room, int week) => new(House.Id, Tenants[tenant], Rooms[room],
            Monday.AddDays(7 * week), Monday.AddDays(7 * week + 6), House.GetTimeZone(), plan.Id);
    }

    public class RotationHandlerTests
    {
        // 11:00 in Rome: today is Wednesday 04/11, week 0 of a plan starting on Monday 02/11
        private static readonly DateTimeOffset Now = new(2026, 11, 4, 10, 0, 0, TimeSpan.Zero);

        private readonly IHouseRepository _houses = Substitute.For<IHouseRepository>();
        private readonly IRotationPlanRepository _plans = Substitute.For<IRotationPlanRepository>();
        private readonly IShiftRepository _shifts = Substitute.For<IShiftRepository>();
        private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
        private readonly List<Notification> _sentNotifications = [];
        private readonly List<Shift> _addedShifts = [];
        private readonly TimeProvider _clock = AuthTestDoubles.Clock(Now);

        public RotationHandlerTests()
        {
            _notifications.TryAddAsync(Arg.Do<Notification>(_sentNotifications.Add), Arg.Any<CancellationToken>()).Returns(true);
            _shifts.GetByRotationPlanAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns(Array.Empty<Shift>());
            _shifts.TryAddRangeAsync(Arg.Do<IReadOnlyCollection<Shift>>(_addedShifts.AddRange), Arg.Any<CancellationToken>())
                .Returns(true);
        }

        private RotationShiftGenerator Generator =>
            new(_shifts, new NotificationPublisher(_notifications, _clock, Substitute.For<IPushDispatcher>()), _clock);

        private SaveRotationPlanCommandHandler SaveHandler => new(_houses, _plans, Generator);

        private RotationScenario Arrange(int members = 3, int rooms = 3)
        {
            var scenario = RotationScenario.Create(members, rooms);
            _houses.GetByIdAsync(scenario.House.Id, Arg.Any<CancellationToken>()).Returns(scenario.House);
            return scenario;
        }

        private static SaveRotationPlanCommand SaveCommand(RotationScenario s, Guid requester, IEnumerable<Tenant>? tenants = null) => new(
            s.House.Id, (tenants ?? s.Tenants).Select(t => t.Id).ToList(), s.Rooms.Select(r => r.Id).ToList(),
            RotationScenario.Monday, requester);

        [Fact]
        public async Task Save_Should_CreatePlan_AndGenerateTheShiftsOfCurrentAndNextWeek()
        {
            var s = Arrange();

            var response = await SaveHandler.Handle(SaveCommand(s, s.Users[0].Id), CancellationToken.None);

            await _plans.Received(1).AddAsync(Arg.Is<RotationPlan>(p => p.HouseId == s.House.Id && p.Id == response.Id),
                Arg.Any<CancellationToken>());
            _addedShifts.Should().HaveCount(6).And.OnlyContain(shift => shift.RotationPlanId == response.Id && shift.HouseId == s.House.Id);
            _addedShifts.Select(x => (x.AssignedTenant.Id, x.AssignedRoom.Id, x.StartDate, x.EndDate)).Should().BeEquivalentTo(new[]
            {
                (s.Tenants[0].Id, s.Rooms[0].Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)),
                (s.Tenants[1].Id, s.Rooms[1].Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)),
                (s.Tenants[2].Id, s.Rooms[2].Id, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8)),
                (s.Tenants[0].Id, s.Rooms[1].Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)),
                (s.Tenants[1].Id, s.Rooms[2].Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)),
                (s.Tenants[2].Id, s.Rooms[0].Id, new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15)),
            });
            // End of Sunday 08/11 in Rome (CET)
            _addedShifts[0].DueDate.Should().Be(new DateTime(2026, 11, 8, 23, 0, 0, DateTimeKind.Utc));
            _addedShifts[0].Checklist.Should().ContainSingle(item => item.Description == "Pulire stanza 1");

            // The requester does not get notified about their own plan
            _sentNotifications.Should().HaveCount(4)
                .And.OnlyContain(n => n.Type == NotificationType.ShiftAssigned && n.RecipientUserId != s.Users[0].Id);

            response.StartDate.Should().Be(RotationScenario.Monday);
            response.Tenants.Select(t => t.Name).Should().Equal(s.Tenants.Select(t => t.Name));
            response.Rooms.Select(r => r.Name).Should().Equal("Stanza 1", "Stanza 2", "Stanza 3");
            response.Weeks.Select(w => w.Index).Should().Equal(0, 1, 2);
            response.Weeks[1].Assignments[0].Should().Be(
                new CoinquiLindo.Shared.Contracts.RotationAssignmentResponse(s.Tenants[0].Id, s.Tenants[0].Name, s.Rooms[1].Id, "Stanza 2"));
        }

        [Fact]
        public async Task Save_Should_UpdateExistingPlan_AndRebuildOnlyTheWeeksNotStarted()
        {
            var s = Arrange();
            var plan = s.Plan();
            var current = s.RotationShift(plan, 0, 0, week: 0);
            var futureUntouched = s.RotationShift(plan, 0, 1, week: 1);
            _plans.GetByHouseIdAsync(s.House.Id, Arg.Any<CancellationToken>()).Returns(plan);
            _shifts.GetByRotationPlanAsync(plan.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns([current, futureUntouched], [current]);
            var newOrder = new[] { s.Tenants[2], s.Tenants[1], s.Tenants[0] };

            var response = await SaveHandler.Handle(SaveCommand(s, s.Users[0].Id, newOrder), CancellationToken.None);

            response.Id.Should().Be(plan.Id);
            plan.TenantIds.Should().Equal(newOrder.Select(t => t.Id));
            await _plans.Received(1).UpdateAsync(plan, Arg.Any<CancellationToken>());
            await _plans.DidNotReceive().AddAsync(Arg.Any<RotationPlan>(), Arg.Any<CancellationToken>());
            await _shifts.Received(1).RemoveRangeAsync(
                Arg.Is<IReadOnlyCollection<Shift>>(list => list.Count == 1 && list.Single() == futureUntouched), Arg.Any<CancellationToken>());
            // Week 0 is in progress and keeps its shifts; week 1 follows the new order
            _addedShifts.Should().HaveCount(3).And.OnlyContain(x => x.StartDate == new DateOnly(2026, 11, 9));
            _addedShifts.Single(x => x.AssignedTenant == s.Tenants[2]).AssignedRoom.Should().Be(s.Rooms[1]);
        }

        [Fact]
        public async Task Save_Should_KeepFutureShiftsWithActivity()
        {
            var s = Arrange();
            var plan = s.Plan();
            var withCompletedItem = s.RotationShift(plan, 0, 1, week: 1);
            withCompletedItem.MarkItemAsCompleted(withCompletedItem.Checklist.Single().Id);
            var withReport = s.RotationShift(plan, 1, 2, week: 1);
            withReport.AddReport(new Report(s.Tenants[0], "Sporco", "0f8fad5bd9cb469fa165708167e1f3e1.png"));
            _plans.GetByHouseIdAsync(s.House.Id, Arg.Any<CancellationToken>()).Returns(plan);
            _shifts.GetByRotationPlanAsync(plan.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns([withCompletedItem, withReport]);

            await SaveHandler.Handle(SaveCommand(s, s.Users[0].Id), CancellationToken.None);

            await _shifts.DidNotReceive().RemoveRangeAsync(Arg.Any<IReadOnlyCollection<Shift>>(), Arg.Any<CancellationToken>());
            // Week 1 already has shifts of the plan: it is not generated twice
            _addedShifts.Should().OnlyContain(x => x.StartDate == RotationScenario.Monday);
        }

        [Fact]
        public async Task Save_Should_ThrowNotFound_ForUnknownHouse_AndAccessDenied_ForNonMembers()
        {
            var s = Arrange();

            var unknownHouse = () => SaveHandler.Handle(SaveCommand(s, s.Users[0].Id) with { HouseId = Guid.NewGuid() }, CancellationToken.None);
            var outsider = () => SaveHandler.Handle(SaveCommand(s, Guid.NewGuid()), CancellationToken.None);

            (await unknownHouse.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("House.NotFound");
            (await outsider.Should().ThrowAsync<NotHouseMemberException>()).Which.Code.Should().Be("House.AccessDenied");
            await _plans.DidNotReceive().AddAsync(Arg.Any<RotationPlan>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Save_Should_PropagateValidationErrors_WithoutSavingAnything()
        {
            var s = Arrange();

            var action = () => SaveHandler.Handle(SaveCommand(s, s.Users[0].Id, []), CancellationToken.None);

            (await action.Should().ThrowAsync<DomainValidationException>()).Which.Code.Should().Be("RotationPlan.TenantsRequired");
            await _plans.DidNotReceive().AddAsync(Arg.Any<RotationPlan>(), Arg.Any<CancellationToken>());
            _addedShifts.Should().BeEmpty();
        }

        [Fact]
        public async Task Delete_Should_RemovePlan_AndShiftsNotStarted()
        {
            var s = Arrange();
            var plan = s.Plan();
            var current = s.RotationShift(plan, 0, 0, week: 0);
            var future = s.RotationShift(plan, 0, 1, week: 1);
            _plans.GetByHouseIdAsync(s.House.Id, Arg.Any<CancellationToken>()).Returns(plan);
            _shifts.GetByRotationPlanAsync(plan.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns([current, future]);

            await new DeleteRotationPlanCommandHandler(_houses, _plans, Generator)
                .Handle(new DeleteRotationPlanCommand(s.House.Id, s.Users[1].Id), CancellationToken.None);

            await _shifts.Received(1).RemoveRangeAsync(
                Arg.Is<IReadOnlyCollection<Shift>>(list => list.Count == 1 && list.Single() == future), Arg.Any<CancellationToken>());
            await _plans.Received(1).RemoveAsync(plan, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Delete_Should_BeNoOp_When_HouseHasNoPlan_AndRequireMembership()
        {
            var s = Arrange();
            var handler = new DeleteRotationPlanCommandHandler(_houses, _plans, Generator);

            await handler.Handle(new DeleteRotationPlanCommand(s.House.Id, s.Users[0].Id), CancellationToken.None);
            var outsider = () => handler.Handle(new DeleteRotationPlanCommand(s.House.Id, Guid.NewGuid()), CancellationToken.None);

            await _plans.DidNotReceive().RemoveAsync(Arg.Any<RotationPlan>(), Arg.Any<CancellationToken>());
            await outsider.Should().ThrowAsync<NotHouseMemberException>();
        }

        [Fact]
        public async Task Get_Should_ReturnNull_When_HouseHasNoPlan()
        {
            var s = Arrange();

            var result = await new GetRotationPlanQueryHandler(_houses, _plans, _clock)
                .Handle(new GetRotationPlanQuery(s.House.Id, s.Users[0].Id), CancellationToken.None);

            result.Should().BeNull();
        }

        [Fact]
        public async Task Get_Should_ReturnOneCycleFromTheCurrentWeek_WithRestingTenants()
        {
            var s = Arrange(members: 4, rooms: 3);
            // Started one week earlier: today is in week 1
            var plan = s.Plan(RotationScenario.Monday.AddDays(-7));
            _plans.GetByHouseIdAsync(s.House.Id, Arg.Any<CancellationToken>()).Returns(plan);

            var result = await new GetRotationPlanQueryHandler(_houses, _plans, _clock)
                .Handle(new GetRotationPlanQuery(s.House.Id, s.Users[0].Id), CancellationToken.None);

            result!.Weeks.Select(w => w.Index).Should().Equal(1, 2, 3, 4);
            result.Weeks[0].StartDate.Should().Be(RotationScenario.Monday);
            result.Weeks[0].RestingTenants.Should().ContainSingle().Which.Id.Should().Be(s.Tenants[2].Id);
            result.Weeks[0].UnassignedRooms.Should().BeEmpty();
            result.Weeks[0].Assignments.Should().HaveCount(3);
        }

        [Fact]
        public async Task Get_Should_RequireMembership()
        {
            var s = Arrange();

            var action = () => new GetRotationPlanQueryHandler(_houses, _plans, _clock)
                .Handle(new GetRotationPlanQuery(s.House.Id, Guid.NewGuid()), CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
        }

        [Fact]
        public async Task Generate_Should_MaterializeEveryPlan_SkippingWeeksAlreadyGenerated()
        {
            var a = Arrange(members: 2, rooms: 2);
            var b = Arrange(members: 2, rooms: 2);
            var planA = a.Plan();
            var planB = b.Plan();
            _plans.GetAllAsync(Arg.Any<CancellationToken>()).Returns([planA, planB]);
            _shifts.GetByRotationPlanAsync(planA.Id, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
                .Returns([a.RotationShift(planA, 0, 0, week: 0), a.RotationShift(planA, 1, 1, week: 0)]);

            var created = await new GenerateRotationShiftsCommandHandler(_houses, _plans, Generator)
                .Handle(new GenerateRotationShiftsCommand(), CancellationToken.None);

            created.Should().Be(6);
            _addedShifts.Where(x => x.HouseId == a.House.Id).Should().HaveCount(2)
                .And.OnlyContain(x => x.StartDate == new DateOnly(2026, 11, 9));
            _addedShifts.Where(x => x.HouseId == b.House.Id).Should().HaveCount(4);
            // Generated by the job: every assignee with an account is notified
            _sentNotifications.Should().HaveCount(6);
        }

        [Fact]
        public async Task Generate_Should_SkipPlansOfMissingHouses_AndShiftsAlreadyAddedByAConcurrentRun()
        {
            var s = Arrange(members: 2, rooms: 2);
            var missingHousePlan = RotationScenario.Create(2, 2).Plan();
            _plans.GetAllAsync(Arg.Any<CancellationToken>()).Returns([missingHousePlan, s.Plan()]);
            _shifts.TryAddRangeAsync(Arg.Any<IReadOnlyCollection<Shift>>(), Arg.Any<CancellationToken>()).Returns(false);

            var created = await new GenerateRotationShiftsCommandHandler(_houses, _plans, Generator)
                .Handle(new GenerateRotationShiftsCommand(), CancellationToken.None);

            created.Should().Be(0);
            _sentNotifications.Should().BeEmpty();
        }
    }
}
