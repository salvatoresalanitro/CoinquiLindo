using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Application.Shifts.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class ShiftHelperHandlerTests
    {
        private readonly IHouseRepository _houses = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shifts = Substitute.For<IShiftRepository>();
        private readonly IRotationPlanRepository _plans = Substitute.For<IRotationPlanRepository>();
        private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
        private readonly List<Notification> _sentNotifications = [];

        // 4 tenants, 3 rooms: in week 0 tenant 3 rests; the shift is tenant 0's
        private readonly RotationScenario _s;
        private readonly RotationPlan _plan;
        private readonly Shift _shift;

        public ShiftHelperHandlerTests()
        {
            _notifications.TryAddAsync(Arg.Do<Notification>(_sentNotifications.Add), Arg.Any<CancellationToken>()).Returns(true);
            _s = RotationScenario.Create(members: 4, rooms: 3);
            _plan = _s.Plan();
            _shift = _s.RotationShift(_plan, 0, 0, week: 0);
            _houses.GetByIdAsync(_s.House.Id, Arg.Any<CancellationToken>()).Returns(_s.House);
            _plans.GetByHouseIdAsync(_s.House.Id, Arg.Any<CancellationToken>()).Returns(_plan);
            _shifts.GetByIdAsync(_s.House.Id, _shift.Id, Arg.Any<CancellationToken>()).Returns(_shift);
            _shifts.GetByHouseIdAsync(_s.House.Id, Arg.Any<CancellationToken>()).Returns([_shift]);
        }

        private Guid Assignee => _s.Users[0].Id;

        private Task Set(Guid helperTenantId, Guid requester, Guid? shiftId = null) =>
            new SetShiftHelperCommandHandler(_houses, _shifts, _plans,
                    new NotificationPublisher(_notifications, TimeProvider.System, Substitute.For<IPushDispatcher>()))
                .Handle(new SetShiftHelperCommand(_s.House.Id, shiftId ?? _shift.Id, helperTenantId, requester), CancellationToken.None);

        private Task Remove(Guid requester) =>
            new RemoveShiftHelperCommandHandler(_houses, _shifts)
                .Handle(new RemoveShiftHelperCommand(_s.House.Id, _shift.Id, requester), CancellationToken.None);

        [Fact]
        public async Task Set_Should_StoreRestingTenantAsHelper_AndNotifyThem()
        {
            await Set(_s.Tenants[3].Id, Assignee);

            _shift.HelperTenant.Should().Be(_s.Tenants[3]);
            await _shifts.Received(1).UpdateAsync(_shift, Arg.Any<CancellationToken>());
            var notification = _sentNotifications.Should().ContainSingle().Which;
            notification.Type.Should().Be(NotificationType.ShiftHelperChosen);
            notification.RecipientUserId.Should().Be(_s.Users[3].Id);
            notification.ShiftId.Should().Be(_shift.Id);
            notification.Body.Should().Contain(_s.Tenants[0].Name).And.Contain("Stanza 1");
        }

        [Fact]
        public async Task Set_Should_Throw_When_HelperIsNotRestingThatWeek()
        {
            var action = () => Set(_s.Tenants[1].Id, Assignee);

            (await action.Should().ThrowAsync<ShiftHelperNotAllowedException>()).Which.Code.Should().Be("Shift.HelperNotAllowed");
            _shift.HelperTenant.Should().BeNull();
            await _shifts.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Set_Should_Throw_When_RequesterIsNotTheAssignee()
        {
            var action = () => Set(_s.Tenants[3].Id, _s.Users[1].Id);

            await action.Should().ThrowAsync<ShiftHelperNotAllowedException>();
            _sentNotifications.Should().BeEmpty();
        }

        [Fact]
        public async Task Set_Should_Throw_When_ThePlanNoLongerExists()
        {
            _plans.GetByHouseIdAsync(_s.House.Id, Arg.Any<CancellationToken>()).Returns((RotationPlan?)null);

            var action = () => Set(_s.Tenants[3].Id, Assignee);

            await action.Should().ThrowAsync<ShiftHelperNotAllowedException>();
        }

        [Fact]
        public async Task Set_Should_ThrowNotFound_ForUnknownShiftOrTenant_AndAccessDenied_ForNonMembers()
        {
            (await FluentActions.Awaiting(() => Set(_s.Tenants[3].Id, Assignee, Guid.NewGuid()))
                .Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("Shift.NotFound");
            (await FluentActions.Awaiting(() => Set(Guid.NewGuid(), Assignee))
                .Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("Tenant.NotFound");
            await FluentActions.Awaiting(() => Set(_s.Tenants[3].Id, Guid.NewGuid()))
                .Should().ThrowAsync<NotHouseMemberException>();
        }

        [Fact]
        public async Task Remove_Should_BeAllowedToTheHelper_AndRejectOtherTenants()
        {
            _shift.SetHelper(_s.Tenants[0].Id, _s.Tenants[3]);

            await FluentActions.Awaiting(() => Remove(_s.Users[1].Id)).Should().ThrowAsync<ShiftHelperNotAllowedException>();
            await Remove(_s.Users[3].Id);

            _shift.HelperTenant.Should().BeNull();
            await _shifts.Received(1).UpdateAsync(_shift, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetHouseShifts_Should_ExposeHelperCandidates_OnlyToTheAssignee()
        {
            var handler = new GetHouseShiftsQueryHandler(_houses, _shifts, _plans);

            var forAssignee = (await handler.Handle(new(_s.House.Id, Assignee), CancellationToken.None)).Single();
            var forOther = (await handler.Handle(new(_s.House.Id, _s.Users[1].Id), CancellationToken.None)).Single();

            forAssignee.IsFromRotation.Should().BeTrue();
            forAssignee.HelperCandidates.Should().ContainSingle().Which.Id.Should().Be(_s.Tenants[3].Id);
            forAssignee.HelperTenantId.Should().BeNull();
            forAssignee.CanRemoveHelper.Should().BeFalse();
            forOther.IsFromRotation.Should().BeTrue();
            forOther.HelperCandidates.Should().BeEmpty();
        }

        [Fact]
        public async Task GetHouseShifts_Should_ExposeHelper_AndLetAssigneeAndHelperRemoveIt()
        {
            _shift.SetHelper(_s.Tenants[0].Id, _s.Tenants[3]);
            var handler = new GetHouseShiftsQueryHandler(_houses, _shifts, _plans);

            var forHelper = (await handler.Handle(new(_s.House.Id, _s.Users[3].Id), CancellationToken.None)).Single();
            var forAssignee = (await handler.Handle(new(_s.House.Id, Assignee), CancellationToken.None)).Single();
            var forOther = (await handler.Handle(new(_s.House.Id, _s.Users[1].Id), CancellationToken.None)).Single();

            forHelper.HelperTenantId.Should().Be(_s.Tenants[3].Id);
            forHelper.HelperTenantName.Should().Be(_s.Tenants[3].Name);
            forHelper.CanRemoveHelper.Should().BeTrue();
            forAssignee.CanRemoveHelper.Should().BeTrue();
            forOther.CanRemoveHelper.Should().BeFalse();
        }

        [Fact]
        public async Task GetHouseShifts_Should_OfferNoCandidates_ForManualShifts()
        {
            var manual = new Shift(_s.House.Id, _s.Tenants[0], _s.Rooms[0], RotationScenario.Monday,
                RotationScenario.Monday.AddDays(6), TimeZoneInfo.Utc);
            _shifts.GetByHouseIdAsync(_s.House.Id, Arg.Any<CancellationToken>()).Returns([manual]);

            var result = (await new GetHouseShiftsQueryHandler(_houses, _shifts, _plans)
                .Handle(new(_s.House.Id, Assignee), CancellationToken.None)).Single();

            result.IsFromRotation.Should().BeFalse();
            result.HelperCandidates.Should().BeEmpty();
        }
    }
}
