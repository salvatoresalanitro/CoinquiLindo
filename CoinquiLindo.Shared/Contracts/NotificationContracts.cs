using System.Text.Json.Serialization;

namespace CoinquiLindo.Shared.Contracts
{
    // Serialized by name; numeric values mirror the domain NotificationType
    [JsonConverter(typeof(JsonStringEnumConverter<NotificationKind>))]
    public enum NotificationKind
    {
        ShiftAssigned = 1,
        ShiftDueSoon = 2,
        ReportCreated = 3,
        ReportVoted = 4
    }

    public record NotificationResponse(
        Guid Id,
        NotificationKind Type,
        string Title,
        string Body,
        Guid HouseId,
        Guid? ShiftId,
        Guid? ReportId,
        DateTime CreatedAt,
        DateTime? ReadAt);

    public record UnreadNotificationsResponse(int Count);
}
