namespace CoinquiLindo.Application.Notifications.Commands
{
    public record MarkNotificationAsReadCommand(Guid NotificationId, Guid RequesterUserId);
}
