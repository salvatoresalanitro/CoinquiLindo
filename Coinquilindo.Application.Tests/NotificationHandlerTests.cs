using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Notifications.Commands;
using CoinquiLindo.Application.Notifications.Queries;
using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Reports.Commands;
using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class NotificationHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
        private static readonly byte[] PngPhoto = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13];

        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();
        private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
        private readonly IPushDispatcher _pushDispatcher = Substitute.For<IPushDispatcher>();
        private readonly TimeProvider _clock = AuthTestDoubles.Clock(Now);
        private readonly List<Notification> _added = [];

        public NotificationHandlerTests()
        {
            _notifications.TryAddAsync(Arg.Do<Notification>(_added.Add), Arg.Any<CancellationToken>()).Returns(true);
            _notifications.TryUpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        private NotificationPublisher Publisher => new(_notifications, _clock, _pushDispatcher);

        [Fact]
        public async Task StoredNotifications_Should_BeQueuedForPush()
        {
            var scenario = Arrange();
            var shift = scenario.CreateShift();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);

            await new CreateShiftReportCommandHandler(_houseRepository, _shiftRepository,
                    Substitute.For<IReportPhotoStorage>(), Publisher)
                .Handle(new CreateShiftReportCommand(scenario.House.Id, shift.Id, "Pavimento sporco",
                    new MemoryStream(PngPhoto), scenario.Reporter.Id), CancellationToken.None);

            _added.Should().HaveCount(2);
            foreach (var notification in _added)
            {
                _pushDispatcher.Received(1).Enqueue(notification.Id);
            }
        }

        [Fact]
        public async Task NotificationsThatCannotBeStored_Should_NotBePushed()
        {
            var scenario = Arrange();
            _notifications.TryAddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(false);

            await new AssignShiftCommandHandler(_houseRepository, _shiftRepository, Publisher).Handle(
                new AssignShiftCommand(scenario.House.Id, RoomId: scenario.Room.Id, TenantId: scenario.AssigneeTenant.Id,
                    Now.UtcDateTime, Now.UtcDateTime.AddDays(7), scenario.Reporter.Id),
                CancellationToken.None);

            _pushDispatcher.DidNotReceive().Enqueue(Arg.Any<Guid>());
        }

        [Fact]
        public async Task RefreshedVoteNotification_Should_BePushedAgain()
        {
            var scenario = Arrange();
            var (shift, report) = scenario.CreateShiftWithReport();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            var existing = new Notification(scenario.Reporter.Id, NotificationType.ReportVoted, scenario.House.Id,
                shift.Id, report.Id, "Nuovi voti sulla tua segnalazione", "vecchio testo", Now.UtcDateTime.AddHours(-1));
            _notifications.GetUnreadForReportAsync(scenario.Reporter.Id, NotificationType.ReportVoted, report.Id, Arg.Any<CancellationToken>())
                .Returns(existing);

            await CastVoteAsync(scenario, shift, report, ReportVoteValue.Up);

            _pushDispatcher.Received(1).Enqueue(existing.Id);
        }

        [Fact]
        public async Task AssignShift_Should_NotifyAssignee_When_AssignedBySomeoneElse()
        {
            var scenario = Arrange();

            var shiftId = await new AssignShiftCommandHandler(_houseRepository, _shiftRepository, Publisher).Handle(
                new AssignShiftCommand(scenario.House.Id, RoomId: scenario.Room.Id, TenantId: scenario.AssigneeTenant.Id,
                    Now.UtcDateTime, Now.UtcDateTime.AddDays(7), scenario.Reporter.Id),
                CancellationToken.None);

            var notification = _added.Should().ContainSingle().Subject;
            notification.RecipientUserId.Should().Be(scenario.Assignee.Id);
            notification.Type.Should().Be(NotificationType.ShiftAssigned);
            notification.HouseId.Should().Be(scenario.House.Id);
            notification.ShiftId.Should().Be(shiftId);
            notification.Body.Should().Contain("Bagno");
            notification.CreatedAt.Should().Be(Now.UtcDateTime);
        }

        [Fact]
        public async Task AssignShift_Should_NotNotify_When_AssigneeIsTheRequester()
        {
            var scenario = Arrange();

            await new AssignShiftCommandHandler(_houseRepository, _shiftRepository, Publisher).Handle(
                new AssignShiftCommand(scenario.House.Id, RoomId: scenario.Room.Id, TenantId: scenario.AssigneeTenant.Id,
                    Now.UtcDateTime, Now.UtcDateTime.AddDays(7), scenario.Assignee.Id),
                CancellationToken.None);

            _added.Should().BeEmpty();
        }

        [Fact]
        public async Task AssignShift_Should_NotNotify_TenantWithoutAccount()
        {
            var scenario = Arrange();
            var legacy = new Tenant("Senza account");
            scenario.House.AddTenant(legacy);

            await new AssignShiftCommandHandler(_houseRepository, _shiftRepository, Publisher).Handle(
                new AssignShiftCommand(scenario.House.Id, RoomId: scenario.Room.Id, TenantId: legacy.Id,
                    Now.UtcDateTime, Now.UtcDateTime.AddDays(7), scenario.Reporter.Id),
                CancellationToken.None);

            _added.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateReport_Should_NotifyEveryOtherMember_WithDedicatedTextForAssignee()
        {
            var scenario = Arrange();
            var shift = scenario.CreateShift();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);

            var report = await new CreateShiftReportCommandHandler(_houseRepository, _shiftRepository,
                    Substitute.For<IReportPhotoStorage>(), Publisher)
                .Handle(new CreateShiftReportCommand(scenario.House.Id, shift.Id, "Pavimento sporco",
                    new MemoryStream(PngPhoto), scenario.Reporter.Id), CancellationToken.None);

            _added.Should().HaveCount(2).And.OnlyContain(n =>
                n.Type == NotificationType.ReportCreated && n.ShiftId == shift.Id && n.ReportId == report.Id
                && n.HouseId == scenario.House.Id && n.Body.Contains("Pavimento sporco"));
            _added.Should().NotContain(n => n.RecipientUserId == scenario.Reporter.Id);
            _added.Single(n => n.RecipientUserId == scenario.Assignee.Id).Title.Should().Be("Nuova segnalazione sul tuo turno");
            _added.Single(n => n.RecipientUserId == scenario.Voter.Id).Title.Should().Be("Nuova segnalazione");
        }

        [Fact]
        public async Task CastVote_Should_NotifyReporter_OnFirstVoteOfTheTenant()
        {
            var scenario = Arrange();
            var (shift, report) = scenario.CreateShiftWithReport();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);

            await CastVoteAsync(scenario, shift, report, ReportVoteValue.Up);

            var notification = _added.Should().ContainSingle().Subject;
            notification.RecipientUserId.Should().Be(scenario.Reporter.Id);
            notification.Type.Should().Be(NotificationType.ReportVoted);
            notification.ReportId.Should().Be(report.Id);
            notification.Body.Should().Contain("1 d'accordo").And.Contain("0 in disaccordo");
        }

        [Fact]
        public async Task CastVote_Should_RefreshExistingUnreadVoteNotification_InsteadOfAddingANewOne()
        {
            var scenario = Arrange();
            var (shift, report) = scenario.CreateShiftWithReport();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            var existing = new Notification(scenario.Reporter.Id, NotificationType.ReportVoted, scenario.House.Id,
                shift.Id, report.Id, "Nuovi voti sulla tua segnalazione", "vecchio testo", Now.UtcDateTime.AddHours(-1));
            _notifications.GetUnreadForReportAsync(scenario.Reporter.Id, NotificationType.ReportVoted, report.Id, Arg.Any<CancellationToken>())
                .Returns(existing);

            await CastVoteAsync(scenario, shift, report, ReportVoteValue.Down);

            _added.Should().BeEmpty();
            existing.Body.Should().Contain("0 d'accordo").And.Contain("1 in disaccordo");
            existing.CreatedAt.Should().Be(Now.UtcDateTime);
            await _notifications.Received(1).TryUpdateAsync(existing, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task CastVote_Should_NotNotify_When_TenantOnlyChangesItsVote()
        {
            var scenario = Arrange();
            var (shift, report) = scenario.CreateShiftWithReport();
            shift.VoteReport(report.Id, scenario.VoterTenant, VoteType.Up);
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);

            await CastVoteAsync(scenario, shift, report, ReportVoteValue.Down);

            _added.Should().BeEmpty();
            await _notifications.DidNotReceive().TryUpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handlers_Should_Succeed_When_NotificationCannotBeStored()
        {
            var scenario = Arrange();
            var (shift, report) = scenario.CreateShiftWithReport();
            _shiftRepository.GetByIdAsync(scenario.House.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            _notifications.TryAddAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(false);

            var response = await CastVoteAsync(scenario, shift, report, ReportVoteValue.Up);

            response.Score.Should().Be(1);
        }

        [Fact]
        public async Task ShiftDueSoon_Should_NotifyAssigneesOfIncompleteShiftsEndingWithinLeadTime_Once()
        {
            var scenario = Arrange();
            var incomplete = scenario.CreateShift(endsAt: Now.UtcDateTime.AddHours(5));
            var completed = scenario.CreateShift(endsAt: Now.UtcDateTime.AddHours(6));
            completed.MarkItemAsCompleted(completed.Checklist.Single().Id);
            var alreadyNotified = scenario.CreateShift(endsAt: Now.UtcDateTime.AddHours(7));
            var legacyShift = new Shift(scenario.House.Id, new Tenant("Senza account"), scenario.Room,
                Now.UtcDateTime.AddDays(-6), Now.UtcDateTime.AddHours(8));
            _shiftRepository.GetEndingBetweenAsync(Now.UtcDateTime, Now.UtcDateTime.AddHours(24), Arg.Any<CancellationToken>())
                .Returns([incomplete, completed, alreadyNotified, legacyShift]);
            _notifications.ExistsForShiftAsync(scenario.Assignee.Id, NotificationType.ShiftDueSoon, alreadyNotified.Id, Arg.Any<CancellationToken>())
                .Returns(true);

            var created = await new CreateShiftDueSoonNotificationsCommandHandler(_shiftRepository, Publisher, _clock)
                .Handle(new CreateShiftDueSoonNotificationsCommand(TimeSpan.FromHours(24)), CancellationToken.None);

            created.Should().Be(1);
            var notification = _added.Should().ContainSingle().Subject;
            notification.RecipientUserId.Should().Be(scenario.Assignee.Id);
            notification.Type.Should().Be(NotificationType.ShiftDueSoon);
            notification.ShiftId.Should().Be(incomplete.Id);
            notification.Body.Should().Contain("Bagno");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task ShiftDueSoon_Should_RejectNonPositiveLeadTime(int hours)
        {
            var action = () => new CreateShiftDueSoonNotificationsCommandHandler(_shiftRepository, Publisher, _clock)
                .Handle(new CreateShiftDueSoonNotificationsCommand(TimeSpan.FromHours(hours)), CancellationToken.None);

            await action.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Theory]
        [InlineData(0, 1)]
        [InlineData(10, 10)]
        [InlineData(500, 50)]
        public async Task GetMyNotifications_Should_MapResponses_AndClampPageSize(int take, int expectedTake)
        {
            var userId = Guid.NewGuid();
            var notification = new Notification(userId, NotificationType.ReportVoted, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), "Titolo", "Testo", Now.UtcDateTime);
            _notifications.GetForRecipientAsync(userId, true, expectedTake, Arg.Any<CancellationToken>()).Returns([notification]);

            var result = await new GetMyNotificationsQueryHandler(_notifications)
                .Handle(new GetMyNotificationsQuery(userId, UnreadOnly: true, Take: take), CancellationToken.None);

            result.Should().ContainSingle().Which.Should().BeEquivalentTo(new NotificationResponse(
                notification.Id, NotificationKind.ReportVoted, "Titolo", "Testo", notification.HouseId,
                notification.ShiftId, notification.ReportId, Now.UtcDateTime, null));
        }

        [Fact]
        public async Task GetUnreadNotificationsCount_Should_ReturnRepositoryCount()
        {
            var userId = Guid.NewGuid();
            _notifications.CountUnreadAsync(userId, Arg.Any<CancellationToken>()).Returns(3);

            var result = await new GetUnreadNotificationsCountQueryHandler(_notifications)
                .Handle(new GetUnreadNotificationsCountQuery(userId), CancellationToken.None);

            result.Should().Be(new UnreadNotificationsResponse(3));
        }

        [Fact]
        public async Task MarkAsRead_Should_SetReadAtAndSave()
        {
            var userId = Guid.NewGuid();
            var notification = new Notification(userId, NotificationType.ShiftAssigned, Guid.NewGuid(), Guid.NewGuid(),
                null, "Titolo", "Testo", Now.UtcDateTime.AddHours(-1));
            _notifications.GetByIdAsync(userId, notification.Id, Arg.Any<CancellationToken>()).Returns(notification);

            await new MarkNotificationAsReadCommandHandler(_notifications, _clock)
                .Handle(new MarkNotificationAsReadCommand(notification.Id, userId), CancellationToken.None);

            notification.ReadAt.Should().Be(Now.UtcDateTime);
            await _notifications.Received(1).UpdateAsync(notification, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MarkAsRead_Should_ThrowNotFound_When_NotificationBelongsToSomeoneElse()
        {
            var action = () => new MarkNotificationAsReadCommandHandler(_notifications, _clock)
                .Handle(new MarkNotificationAsReadCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "Notification.NotFound");
            await _notifications.DidNotReceive().UpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task MarkAllAsRead_Should_MarkUnreadNotificationsOfTheRequester()
        {
            var userId = Guid.NewGuid();

            await new MarkAllNotificationsAsReadCommandHandler(_notifications, _clock)
                .Handle(new MarkAllNotificationsAsReadCommand(userId), CancellationToken.None);

            await _notifications.Received(1).MarkAllAsReadAsync(userId, Now.UtcDateTime, Arg.Any<CancellationToken>());
        }

        private Task<ReportResponse> CastVoteAsync(Scenario scenario, Shift shift, Report report, ReportVoteValue vote) =>
            new CastReportVoteCommandHandler(_houseRepository, _shiftRepository, Publisher).Handle(
                new CastReportVoteCommand(scenario.House.Id, shift.Id, report.Id, vote, scenario.Voter.Id),
                CancellationToken.None);

        private Scenario Arrange()
        {
            var house = new House("Casa");
            var assignee = TestData.NewUser("Lucia");
            var reporter = TestData.NewUser("Salvo");
            var voter = TestData.NewUser("Marco");
            var assigneeTenant = house.AddMember(assignee);
            var reporterTenant = house.AddMember(reporter);
            var voterTenant = house.AddMember(voter);
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Lavare il pavimento"));
            house.AddRoom(room);
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            return new Scenario(house, room, assignee, reporter, voter, assigneeTenant, reporterTenant, voterTenant);
        }

        private sealed record Scenario(
            House House, Room Room, User Assignee, User Reporter, User Voter,
            Tenant AssigneeTenant, Tenant ReporterTenant, Tenant VoterTenant)
        {
            public Shift CreateShift(DateTime? endsAt = null)
            {
                var end = endsAt ?? Now.UtcDateTime;
                return new Shift(House.Id, AssigneeTenant, Room, end.AddDays(-7), end);
            }

            public (Shift Shift, Report Report) CreateShiftWithReport()
            {
                var shift = CreateShift();
                var report = new Report(ReporterTenant, "Pavimento sporco", "0f8fad5bd9cb469fa165708167e1f3e1.png");
                shift.AddReport(report);
                return (shift, report);
            }
        }
    }
}
