using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class CleaningTaskHandlerTests
    {
        // 23:30 UTC on 15/11 is already 16/11 in Rome: "today" must be the house's day
        private static readonly DateTimeOffset Now = new(2026, 11, 15, 23, 30, 0, TimeSpan.Zero);
        private static readonly DateOnly TodayInRome = new(2026, 11, 16);

        private readonly IHouseRepository _houses = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shifts = Substitute.For<IShiftRepository>();
        private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
        private readonly List<Notification> _added = [];
        private readonly TimeProvider _clock = AuthTestDoubles.Clock(Now);

        private readonly House _house;
        private readonly User _author;
        private readonly User _flatmate;
        private readonly Room _kitchen;
        private readonly Shift _futureShift;

        public CleaningTaskHandlerTests()
        {
            (_house, _author) = TestData.HouseWithMember("Casa", "Europe/Rome");
            _flatmate = TestData.NewUser("Lucia");
            _house.AddMember(_flatmate);
            _house.AddTenant(new Tenant("Senza account"));
            _kitchen = new Room("Cucina");
            _kitchen.AddCleaningTask(new CleaningTask("Lavare i piatti"));
            _house.AddRoom(_kitchen);
            _futureShift = new Shift(_house.Id, _house.Tenants.First(), _kitchen,
                TodayInRome.AddDays(1), TodayInRome.AddDays(7), _house.GetTimeZone());

            _houses.GetByIdAsync(_house.Id, Arg.Any<CancellationToken>()).Returns(_house);
            _shifts.GetByRoomStartingAfterAsync(_house.Id, _kitchen.Id, TodayInRome, Arg.Any<CancellationToken>())
                .Returns([_futureShift]);
            _notifications.TryAddAsync(Arg.Do<Notification>(_added.Add), Arg.Any<CancellationToken>()).Returns(true);
            _notifications.TryUpdateAsync(Arg.Any<Notification>(), Arg.Any<CancellationToken>()).Returns(true);
        }

        private NotificationPublisher Publisher => new(_notifications, _clock, Substitute.For<IPushDispatcher>());

        private Task<Guid> Add(string description, Guid? requester = null, Guid? roomId = null) =>
            new AddCleaningTaskCommandHandler(_houses, _shifts, Publisher, _clock).Handle(
                new AddCleaningTaskCommand(_house.Id, roomId ?? _kitchen.Id, description, requester ?? _author.Id), CancellationToken.None);

        private Task Update(Guid taskId, string description) =>
            new UpdateCleaningTaskCommandHandler(_houses, _shifts, Publisher, _clock).Handle(
                new UpdateCleaningTaskCommand(_house.Id, _kitchen.Id, taskId, description, _author.Id), CancellationToken.None);

        private Task Remove(Guid taskId) =>
            new RemoveCleaningTaskCommandHandler(_houses, _shifts, Publisher, _clock).Handle(
                new RemoveCleaningTaskCommand(_house.Id, _kitchen.Id, taskId, _author.Id), CancellationToken.None);

        private Guid ExistingTaskId => _kitchen.CleaningTasks.Single(t => t.Description == "Lavare i piatti").Id;

        [Fact]
        public async Task Add_Should_SaveTheTask_UpdateShiftsNotStarted_AndNotifyTheOtherFlatmates()
        {
            var taskId = await Add("  Pulire i vetri ");

            _kitchen.CleaningTasks.Should().Contain(t => t.Id == taskId && t.Description == "Pulire i vetri");
            await _houses.Received(1).UpdateAsync(_house, Arg.Any<CancellationToken>());
            _futureShift.Checklist.Should().Contain(i => i.CleaningTaskId == taskId && i.Description == "Pulire i vetri");
            await _shifts.Received(1).UpdateAsync(_futureShift, Arg.Any<CancellationToken>());

            // Only the flatmate with an account who did not make the change
            var notification = _added.Should().ContainSingle().Which;
            notification.RecipientUserId.Should().Be(_flatmate.Id);
            notification.Type.Should().Be(NotificationType.CleaningTasksChanged);
            notification.HouseId.Should().Be(_house.Id);
            notification.Body.Should().Be("Salvo ha aggiunto «Pulire i vetri» in Cucina.");
        }

        [Fact]
        public async Task Update_Should_RenameTheTaskAndTheItemsOfShiftsNotStarted_AndNotify()
        {
            await Update(ExistingTaskId, "Lavare e asciugare i piatti");

            _kitchen.CleaningTasks.Single().Description.Should().Be("Lavare e asciugare i piatti");
            _futureShift.Checklist.Single().Description.Should().Be("Lavare e asciugare i piatti");
            _added.Single().Body.Should().Be("Salvo ha modificato «Lavare i piatti» in «Lavare e asciugare i piatti» (Cucina).");
        }

        [Fact]
        public async Task Remove_Should_RemoveTheTaskAndTheItemsOfShiftsNotStarted_AndNotify()
        {
            await Remove(ExistingTaskId);

            _kitchen.CleaningTasks.Should().BeEmpty();
            _futureShift.Checklist.Should().BeEmpty();
            _added.Single().Body.Should().Be("Salvo ha eliminato «Lavare i piatti» da Cucina.");
        }

        [Fact]
        public async Task Notifications_Should_BeGrouped_InTheUnreadOneOfTheHouse()
        {
            var unread = new Notification(_flatmate.Id, NotificationType.CleaningTasksChanged, _house.Id, null, null,
                "Attività delle stanze aggiornate", "Salvo ha aggiunto «Spazzare» in Cucina.", Now.UtcDateTime.AddHours(-1));
            _notifications.GetUnreadForHouseAsync(_flatmate.Id, NotificationType.CleaningTasksChanged, _house.Id, Arg.Any<CancellationToken>())
                .Returns(unread);

            await Add("Pulire i vetri");

            _added.Should().BeEmpty();
            await _notifications.Received(1).TryUpdateAsync(unread, Arg.Any<CancellationToken>());
            unread.Body.Should().Be("Salvo ha aggiunto «Pulire i vetri» in Cucina.\nSalvo ha aggiunto «Spazzare» in Cucina.");
            unread.CreatedAt.Should().Be(Now.UtcDateTime);
        }

        [Fact]
        public async Task Errors_Should_LeaveEverythingUntouched()
        {
            (await FluentActions.Awaiting(() => Add("Lavare I Piatti")).Should().ThrowAsync<CleaningTaskAlreadyExistsException>())
                .Which.Code.Should().Be("Room.CleaningTaskAlreadyExists");
            (await FluentActions.Awaiting(() => Add("  ")).Should().ThrowAsync<DomainValidationException>())
                .Which.Code.Should().Be("CleaningTask.DescriptionRequired");
            (await FluentActions.Awaiting(() => Add("Spazzare", roomId: Guid.NewGuid())).Should().ThrowAsync<NotFoundException>())
                .Which.Code.Should().Be("Room.NotFound");
            (await FluentActions.Awaiting(() => Update(Guid.NewGuid(), "Spazzare")).Should().ThrowAsync<NotFoundException>())
                .Which.Code.Should().Be("CleaningTask.NotFound");
            (await FluentActions.Awaiting(() => Remove(Guid.NewGuid())).Should().ThrowAsync<NotFoundException>())
                .Which.Code.Should().Be("CleaningTask.NotFound");
            await FluentActions.Awaiting(() => Add("Spazzare", requester: Guid.NewGuid())).Should().ThrowAsync<NotHouseMemberException>();

            await _houses.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
            await _shifts.DidNotReceive().UpdateAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
            _added.Should().BeEmpty();
        }

        [Fact]
        public async Task Add_Should_ThrowNotFound_ForUnknownHouse()
        {
            var action = () => new AddCleaningTaskCommandHandler(_houses, _shifts, Publisher, _clock).Handle(
                new AddCleaningTaskCommand(Guid.NewGuid(), _kitchen.Id, "Spazzare", _author.Id), CancellationToken.None);

            (await action.Should().ThrowAsync<NotFoundException>()).Which.Code.Should().Be("House.NotFound");
        }
    }
}
