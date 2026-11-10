namespace CoinquiLindo.Application.Notifications.Commands
{
    // Sent periodically by the host scheduler; LeadTime comes from configuration
    public record CreateShiftDueSoonNotificationsCommand(TimeSpan LeadTime);
}
