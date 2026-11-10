namespace CoinquiLindo.Application.Notifications.Commands
{
    // Dispatched by the host push worker for each queued notification
    public record SendPushNotificationCommand(Guid NotificationId);
}
