using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Notifications.Commands;
using CoinquiLindo.Application.Users.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class PushHandlerTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);

        private readonly IUserRepository _users = Substitute.For<IUserRepository>();
        private readonly INotificationRepository _notifications = Substitute.For<INotificationRepository>();
        private readonly IPushSender _pushSender = Substitute.For<IPushSender>();
        private readonly TimeProvider _clock = AuthTestDoubles.Clock(Now);

        [Fact]
        public async Task RegisterDevice_Should_AddDeviceToRequester()
        {
            var user = Arrange("Salvo");

            await new RegisterPushDeviceCommandHandler(_users, _clock).Handle(
                new RegisterPushDeviceCommand(user.Id, "fcm-token", PushPlatformValue.Android), CancellationToken.None);

            user.PushDevices.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Token = "fcm-token", Platform = PushPlatform.Android, LastSeenAt = Now.UtcDateTime
            });
            await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RegisterDevice_Should_MoveTokenFromPreviousAccount()
        {
            var previous = Arrange("Lucia");
            previous.RegisterPushDevice("fcm-token", PushPlatform.Android, Now.UtcDateTime.AddDays(-1));
            var user = Arrange("Salvo");
            _users.GetByPushTokenAsync("fcm-token", Arg.Any<CancellationToken>()).Returns(previous);

            await new RegisterPushDeviceCommandHandler(_users, _clock).Handle(
                new RegisterPushDeviceCommand(user.Id, "fcm-token", PushPlatformValue.Android), CancellationToken.None);

            previous.PushDevices.Should().BeEmpty("one physical device receives the pushes of the signed-in account only");
            user.PushDevices.Should().ContainSingle();
        }

        [Fact]
        public async Task RegisterDevice_Should_RejectInvalidToken_WithoutSaving()
        {
            var user = Arrange("Salvo");

            var action = () => new RegisterPushDeviceCommandHandler(_users, _clock).Handle(
                new RegisterPushDeviceCommand(user.Id, " ", PushPlatformValue.Android), CancellationToken.None);

            await action.Should().ThrowAsync<DomainValidationException>().Where(e => e.Code == "PushDevice.TokenRequired");
            await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task RegisterDevice_Should_ThrowUserNotFound_ForUnknownUser()
        {
            var action = () => new RegisterPushDeviceCommandHandler(_users, _clock).Handle(
                new RegisterPushDeviceCommand(Guid.NewGuid(), "fcm-token", PushPlatformValue.Android), CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "User.NotFound");
        }

        [Fact]
        public async Task UnregisterDevice_Should_RemoveRequesterToken()
        {
            var user = Arrange("Salvo");
            user.RegisterPushDevice("fcm-token", PushPlatform.Android, Now.UtcDateTime);

            await new UnregisterPushDeviceCommandHandler(_users).Handle(
                new UnregisterPushDeviceCommand(user.Id, "fcm-token"), CancellationToken.None);

            user.PushDevices.Should().BeEmpty();
            await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SendPush_Should_SendNotificationToAllRecipientDevices_WithNavigationData()
        {
            var user = Arrange("Salvo");
            user.RegisterPushDevice("token-a", PushPlatform.Android, Now.UtcDateTime);
            user.RegisterPushDevice("token-b", PushPlatform.Android, Now.UtcDateTime);
            var notification = ArrangeNotification(user);
            _notifications.CountUnreadAsync(user.Id, Arg.Any<CancellationToken>()).Returns(3);
            PushMessage? sent = null;
            _pushSender.SendAsync(Arg.Do<PushMessage>(m => sent = m), Arg.Any<CancellationToken>())
                .Returns(new PushSendResult(2, []));

            await SendHandler().Handle(new SendPushNotificationCommand(notification.Id), CancellationToken.None);

            sent.Should().NotBeNull();
            sent!.Tokens.Should().BeEquivalentTo(["token-a", "token-b"]);
            sent.Title.Should().Be(notification.Title);
            sent.Body.Should().Be(notification.Body);
            sent.Tag.Should().Be(notification.Id.ToString());
            sent.OccurredAt.Should().Be(Now.UtcDateTime);
            sent.OccurredAt.Kind.Should().Be(DateTimeKind.Utc);
            sent.Data.Should().Contain(new Dictionary<string, string>
            {
                ["notificationId"] = notification.Id.ToString(),
                ["houseId"] = notification.HouseId.ToString(),
                ["type"] = nameof(NotificationKind.ReportCreated),
                ["unreadCount"] = "3"
            });
            await _users.DidNotReceive().UpdateAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SendPush_Should_RemoveTokensRejectedByTheProvider()
        {
            var user = Arrange("Salvo");
            user.RegisterPushDevice("token-ok", PushPlatform.Android, Now.UtcDateTime);
            user.RegisterPushDevice("token-dead", PushPlatform.Android, Now.UtcDateTime);
            var notification = ArrangeNotification(user);
            _pushSender.SendAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>())
                .Returns(new PushSendResult(1, ["token-dead"]));

            await SendHandler().Handle(new SendPushNotificationCommand(notification.Id), CancellationToken.None);

            user.PushDevices.Should().ContainSingle().Which.Token.Should().Be("token-ok");
            await _users.Received(1).UpdateAsync(user, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task SendPush_Should_Skip_WhenNotificationIsReadMissingOrUserHasNoDevices()
        {
            var withoutDevices = Arrange("Lucia");
            var noDevicesNotification = ArrangeNotification(withoutDevices);
            var withDevice = Arrange("Salvo");
            withDevice.RegisterPushDevice("token-a", PushPlatform.Android, Now.UtcDateTime);
            var readNotification = ArrangeNotification(withDevice);
            readNotification.MarkAsRead(Now.UtcDateTime);

            await SendHandler().Handle(new SendPushNotificationCommand(noDevicesNotification.Id), CancellationToken.None);
            await SendHandler().Handle(new SendPushNotificationCommand(readNotification.Id), CancellationToken.None);
            await SendHandler().Handle(new SendPushNotificationCommand(Guid.NewGuid()), CancellationToken.None);

            await _pushSender.DidNotReceive().SendAsync(Arg.Any<PushMessage>(), Arg.Any<CancellationToken>());
        }

        private SendPushNotificationCommandHandler SendHandler() => new(_notifications, _users, _pushSender);

        private User Arrange(string name)
        {
            var user = TestData.NewUser(name);
            _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
            return user;
        }

        private Notification ArrangeNotification(User recipient)
        {
            // Unspecified kind, as SQLite returns it
            var notification = new Notification(recipient.Id, NotificationType.ReportCreated, Guid.NewGuid(), Guid.NewGuid(),
                Guid.NewGuid(), "Nuova segnalazione", "Marco ha segnalato il turno Bagno",
                DateTime.SpecifyKind(Now.UtcDateTime, DateTimeKind.Unspecified));
            _notifications.FindAsync(notification.Id, Arg.Any<CancellationToken>()).Returns(notification);
            return notification;
        }
    }
}
