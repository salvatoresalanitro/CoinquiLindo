using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class NotificationTests
    {
        private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void CreateNotification_Should_InitializeAsUnread()
        {
            var userId = Guid.NewGuid();
            var houseId = Guid.NewGuid();
            var shiftId = Guid.NewGuid();
            var reportId = Guid.NewGuid();

            var notification = new Notification(userId, NotificationType.ReportCreated, houseId, shiftId, reportId,
                "Nuova segnalazione", "Salvo ha segnalato il turno Bagno", Now);

            notification.Id.Should().NotBeEmpty();
            notification.RecipientUserId.Should().Be(userId);
            notification.Type.Should().Be(NotificationType.ReportCreated);
            notification.HouseId.Should().Be(houseId);
            notification.ShiftId.Should().Be(shiftId);
            notification.ReportId.Should().Be(reportId);
            notification.Title.Should().Be("Nuova segnalazione");
            notification.Body.Should().Be("Salvo ha segnalato il turno Bagno");
            notification.CreatedAt.Should().Be(Now);
            notification.ReadAt.Should().BeNull();
            notification.IsRead.Should().BeFalse();
        }

        [Fact]
        public void CreateNotification_Should_RejectMissingRecipient()
        {
            var action = () => new Notification(Guid.Empty, NotificationType.ShiftAssigned, Guid.NewGuid(), null, null,
                "Titolo", "Testo", Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Notification.RecipientRequired");
        }

        [Fact]
        public void CreateNotification_Should_RejectMissingHouse()
        {
            var action = () => new Notification(Guid.NewGuid(), NotificationType.ShiftAssigned, Guid.Empty, null, null,
                "Titolo", "Testo", Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Notification.HouseRequired");
        }

        [Theory]
        [InlineData(null, "Testo")]
        [InlineData(" ", "Testo")]
        [InlineData("Titolo", null)]
        [InlineData("Titolo", "")]
        public void CreateNotification_Should_RejectBlankText(string? title, string? body)
        {
            var action = () => new Notification(Guid.NewGuid(), NotificationType.ShiftAssigned, Guid.NewGuid(), null, null,
                title!, body!, Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Notification.TextRequired");
        }

        [Fact]
        public void CreateNotification_Should_RejectUndefinedType()
        {
            var action = () => new Notification(Guid.NewGuid(), (NotificationType)99, Guid.NewGuid(), null, null,
                "Titolo", "Testo", Now);

            action.Should().Throw<DomainValidationException>().Which.Code.Should().Be("Notification.InvalidType");
        }

        [Fact]
        public void CreateNotification_Should_TruncateTextToMaximumLengths()
        {
            var notification = new Notification(Guid.NewGuid(), NotificationType.ReportCreated, Guid.NewGuid(), null, null,
                new string('t', Notification.MaxTitleLength + 10), new string('b', Notification.MaxBodyLength + 10), Now);

            notification.Title.Should().HaveLength(Notification.MaxTitleLength).And.EndWith("…");
            notification.Body.Should().HaveLength(Notification.MaxBodyLength).And.EndWith("…");
        }

        [Fact]
        public void MarkAsRead_Should_SetReadAt_OnlyTheFirstTime()
        {
            var notification = CreateVoteNotification();

            notification.MarkAsRead(Now.AddMinutes(5));
            notification.MarkAsRead(Now.AddMinutes(10));

            notification.IsRead.Should().BeTrue();
            notification.ReadAt.Should().Be(Now.AddMinutes(5));
        }

        [Fact]
        public void Refresh_Should_UpdateTextAndMoveNotificationToTop()
        {
            var notification = CreateVoteNotification();

            notification.Refresh("Nuovi voti", "2 d'accordo, 1 in disaccordo", Now.AddHours(1));

            notification.Title.Should().Be("Nuovi voti");
            notification.Body.Should().Be("2 d'accordo, 1 in disaccordo");
            notification.CreatedAt.Should().Be(Now.AddHours(1));
            notification.IsRead.Should().BeFalse();
        }

        [Fact]
        public void Refresh_Should_BeRejected_When_NotificationIsAlreadyRead()
        {
            var notification = CreateVoteNotification();
            notification.MarkAsRead(Now);

            var action = () => notification.Refresh("Nuovi voti", "Testo", Now.AddHours(1));

            action.Should().Throw<InvalidOperationException>();
        }

        private static Notification CreateVoteNotification() =>
            new(Guid.NewGuid(), NotificationType.ReportVoted, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "Nuovi voti", "1 d'accordo, 0 in disaccordo", Now);
    }
}
