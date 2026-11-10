using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Notifications.Queries
{
    public class GetMyNotificationsQueryHandler(INotificationRepository notificationRepository)
    {
        public const int MaxPageSize = 50;

        public async Task<List<NotificationResponse>> Handle(GetMyNotificationsQuery request, CancellationToken cancellationToken)
        {
            var notifications = await notificationRepository.GetForRecipientAsync(
                request.RequesterUserId, request.UnreadOnly, Math.Clamp(request.Take, 1, MaxPageSize), cancellationToken);

            return notifications.Select(notification => notification.ToResponse()).ToList();
        }
    }
}
