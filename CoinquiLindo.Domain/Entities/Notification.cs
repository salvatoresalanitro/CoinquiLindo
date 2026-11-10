using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    // Standalone aggregate: one row per recipient, so read state is per user
    public class Notification
    {
        public const int MaxTitleLength = 120;
        public const int MaxBodyLength = 500;

        public Guid Id { get; private set; }
        public Guid RecipientUserId { get; private set; }
        public NotificationType Type { get; private set; }
        public Guid HouseId { get; private set; }
        public Guid? ShiftId { get; private set; }
        public Guid? ReportId { get; private set; }
        public string Title { get; private set; }
        public string Body { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? ReadAt { get; private set; }

        public bool IsRead => ReadAt is not null;

        public Notification(
            Guid recipientUserId,
            NotificationType type,
            Guid houseId,
            Guid? shiftId,
            Guid? reportId,
            string title,
            string body,
            DateTime createdAt)
        {
            if (recipientUserId == Guid.Empty)
            {
                throw new DomainValidationException("Notification.RecipientRequired", "A notification needs a recipient.");
            }

            if (houseId == Guid.Empty)
            {
                throw new DomainValidationException("Notification.HouseRequired", "A notification must belong to a house.");
            }

            if (!Enum.IsDefined(type))
            {
                throw new DomainValidationException("Notification.InvalidType", "Unknown notification type.");
            }

            Id = Guid.NewGuid();
            RecipientUserId = recipientUserId;
            Type = type;
            HouseId = houseId;
            ShiftId = shiftId;
            ReportId = reportId;
            (Title, Body) = ValidateText(title, body);
            CreatedAt = createdAt;
        }

        public void MarkAsRead(DateTime readAt) => ReadAt ??= readAt;

        // Collapses repeated events (e.g. votes) into the same unread notification, moved back to the top
        public void Refresh(string title, string body, DateTime refreshedAt)
        {
            if (IsRead)
            {
                throw new InvalidOperationException("A read notification cannot be refreshed: create a new one.");
            }

            (Title, Body) = ValidateText(title, body);
            CreatedAt = refreshedAt;
        }

        private static (string Title, string Body) ValidateText(string title, string body)
        {
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body))
            {
                throw new DomainValidationException("Notification.TextRequired", "A notification needs a title and a body.");
            }

            return (Truncate(title.Trim(), MaxTitleLength), Truncate(body.Trim(), MaxBodyLength));
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : string.Concat(value.AsSpan(0, maxLength - 1), "…");

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Notification() { }
#pragma warning restore CS8618
    }
}
