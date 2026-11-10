using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Notifications.Commands
{
    public class SendPushNotificationCommandHandler(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        IPushSender pushSender)
    {
        public async Task Handle(SendPushNotificationCommand request, CancellationToken cancellationToken)
        {
            var notification = await notificationRepository.FindAsync(request.NotificationId, cancellationToken);
            // Already read in-app (e.g. queued while the user was looking at the inbox): nothing to announce
            if (notification is null || notification.IsRead)
            {
                return;
            }

            var user = await userRepository.GetByIdAsync(notification.RecipientUserId, cancellationToken);
            if (user is null || user.PushDevices.Count == 0)
            {
                return;
            }

            var unread = await notificationRepository.CountUnreadAsync(user.Id, cancellationToken);
            var message = new PushMessage(
                user.PushDevices.Select(device => device.Token).ToList(),
                notification.Title,
                notification.Body,
                notification.Id.ToString(),
                // Stored values come back from SQLite with unspecified kind, but are always UTC
                DateTime.SpecifyKind(notification.CreatedAt, DateTimeKind.Utc),
                new Dictionary<string, string>
                {
                    ["notificationId"] = notification.Id.ToString(),
                    ["houseId"] = notification.HouseId.ToString(),
                    ["type"] = ((NotificationKind)(int)notification.Type).ToString(),
                    ["unreadCount"] = unread.ToString(System.Globalization.CultureInfo.InvariantCulture)
                });

            var result = await pushSender.SendAsync(message, cancellationToken);

            var removed = result.InvalidTokens.Count(user.RemovePushDevice);
            if (removed > 0)
            {
                await userRepository.UpdateAsync(user, cancellationToken);
            }
        }
    }
}
