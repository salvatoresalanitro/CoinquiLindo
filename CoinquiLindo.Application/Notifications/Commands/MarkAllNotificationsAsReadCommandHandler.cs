using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Notifications.Commands
{
    public class MarkAllNotificationsAsReadCommandHandler(INotificationRepository notificationRepository, TimeProvider timeProvider)
    {
        public Task Handle(MarkAllNotificationsAsReadCommand request, CancellationToken cancellationToken) =>
            notificationRepository.MarkAllAsReadAsync(request.RequesterUserId, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
    }
}
