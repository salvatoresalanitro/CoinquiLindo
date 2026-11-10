using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Notifications.Commands
{
    public class MarkNotificationAsReadCommandHandler(INotificationRepository notificationRepository, TimeProvider timeProvider)
    {
        public async Task Handle(MarkNotificationAsReadCommand request, CancellationToken cancellationToken)
        {
            // Loaded by recipient: someone else's notification is indistinguishable from a missing one
            var notification = await notificationRepository.GetByIdAsync(request.RequesterUserId, request.NotificationId, cancellationToken)
                ?? throw new NotFoundException(nameof(Notification), request.NotificationId);

            notification.MarkAsRead(timeProvider.GetUtcNow().UtcDateTime);
            await notificationRepository.UpdateAsync(notification, cancellationToken);
        }
    }
}
