using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class NotificationRepository(AppDbContext dbContext, ILogger<NotificationRepository> logger) : INotificationRepository
    {
        private const int SqliteUniqueConstraintError = 2067;

        public async Task<bool> TryAddAsync(Notification notification, CancellationToken cancellationToken = default)
        {
            dbContext.Notifications.Add(notification);
            return await TrySaveAsync(notification, cancellationToken);
        }

        public Task<bool> TryUpdateAsync(Notification notification, CancellationToken cancellationToken = default) =>
            TrySaveAsync(notification, cancellationToken);

        public Task UpdateAsync(Notification notification, CancellationToken cancellationToken = default) =>
            dbContext.SaveChangesAsync(cancellationToken);

        public Task<Notification?> GetByIdAsync(Guid recipientUserId, Guid notificationId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.FirstOrDefaultAsync(
                n => n.Id == notificationId && n.RecipientUserId == recipientUserId, cancellationToken);

        public Task<Notification?> FindAsync(Guid notificationId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        public Task<Notification?> GetUnreadForReportAsync(Guid recipientUserId, NotificationType type, Guid reportId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.FirstOrDefaultAsync(
                n => n.RecipientUserId == recipientUserId && n.Type == type && n.ReportId == reportId && n.ReadAt == null,
                cancellationToken);

        public Task<Notification?> GetUnreadForHouseAsync(Guid recipientUserId, NotificationType type, Guid houseId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.FirstOrDefaultAsync(
                n => n.RecipientUserId == recipientUserId && n.Type == type && n.HouseId == houseId && n.ReadAt == null,
                cancellationToken);

        public Task<bool> ExistsForShiftAsync(Guid recipientUserId, NotificationType type, Guid shiftId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.AnyAsync(
                n => n.RecipientUserId == recipientUserId && n.Type == type && n.ShiftId == shiftId, cancellationToken);

        public async Task<IReadOnlyList<Notification>> GetForRecipientAsync(Guid recipientUserId, bool unreadOnly, int take, CancellationToken cancellationToken = default)
        {
            var query = dbContext.Notifications.AsNoTracking().Where(n => n.RecipientUserId == recipientUserId);
            if (unreadOnly)
            {
                query = query.Where(n => n.ReadAt == null);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .ThenBy(n => n.Id)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<int> CountUnreadAsync(Guid recipientUserId, CancellationToken cancellationToken = default) =>
            dbContext.Notifications.CountAsync(n => n.RecipientUserId == recipientUserId && n.ReadAt == null, cancellationToken);

        // Set-based update: equivalent to MarkAsRead on every unread row, without loading them
        public Task<int> MarkAllAsReadAsync(Guid recipientUserId, DateTime readAt, CancellationToken cancellationToken = default) =>
            dbContext.Notifications
                .Where(n => n.RecipientUserId == recipientUserId && n.ReadAt == null)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.ReadAt, readAt), cancellationToken);

        private async Task<bool> TrySaveAsync(Notification notification, CancellationToken cancellationToken)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintError })
            {
                // Expected under races (duplicate reminder, concurrent vote notifications)
                logger.LogDebug("Notification {Type} for user {UserId} already exists", notification.Type, notification.RecipientUserId);
            }
            catch (Exception ex) when (ex is DbUpdateException or SqliteException)
            {
                logger.LogError(ex, "Unable to store notification {Type} for user {UserId}", notification.Type, notification.RecipientUserId);
            }

            // The scoped DbContext may be reused: never leave a failed notification pending
            dbContext.Entry(notification).State = EntityState.Detached;
            return false;
        }
    }
}
