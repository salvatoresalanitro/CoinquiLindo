using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;

namespace CoinquiLindo.Domain.RepositoriesInterfaces
{
    public interface INotificationRepository
    {
        // Notifications are a side effect: Try* methods never throw on persistence errors, they log and return false
        Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken = default);
        Task<bool> TryUpdateAsync(Notification notification, CancellationToken cancellationToken = default);
        Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default);

        Task<Notification?> GetByIdAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken = default);
        // Background use only (push dispatch): no recipient check
        Task<Notification?> FindAsync(Guid notificationId, CancellationToken cancellationToken = default);
        Task<Notification?> GetUnreadForReportAsync(Guid recipientUserId, NotificationType type, Guid reportId, CancellationToken cancellationToken = default);
        Task<bool> ExistsForShiftAsync(Guid recipientUserId, NotificationType type, Guid shiftId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Notification>> GetForRecipientAsync(Guid recipientUserId, bool unreadOnly, int take, CancellationToken cancellationToken = default);
        Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken = default);
        Task<int> MarkAllAsReadAsync(Guid recipientUserId, DateTime readAt, CancellationToken cancellationToken = default);
    }
}
