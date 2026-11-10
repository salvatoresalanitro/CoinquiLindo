using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Notifications
{
    internal static class NotificationMapping
    {
        public static NotificationResponse ToResponse(this Notification notification) => new(
            notification.Id,
            (NotificationKind)(int)notification.Type,
            notification.Title,
            notification.Body,
            notification.HouseId,
            notification.ShiftId,
            notification.ReportId,
            notification.CreatedAt,
            notification.ReadAt);
    }
}
