using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Notifications.Queries
{
    public class GetUnreadNotificationsCountQueryHandler(INotificationRepository notificationRepository)
    {
        public async Task<UnreadNotificationsResponse> Handle(GetUnreadNotificationsCountQuery request, CancellationToken cancellationToken) =>
            new(await notificationRepository.CountUnreadAsync(request.RequesterUserId, cancellationToken));
    }
}
