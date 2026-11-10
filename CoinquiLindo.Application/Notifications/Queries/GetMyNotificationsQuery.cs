namespace CoinquiLindo.Application.Notifications.Queries
{
    public record GetMyNotificationsQuery(Guid RequesterUserId, bool UnreadOnly, int Take);
}
