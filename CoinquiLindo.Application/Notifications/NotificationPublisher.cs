using System.Globalization;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Notifications
{
    // Called after the main change is saved: a failed notification never fails the user's request
    public class NotificationPublisher(
        INotificationRepository notificationRepository,
        TimeProvider timeProvider,
        IPushDispatcher pushDispatcher)
    {
        private const int DescriptionPreviewLength = 100;

        public async Task ShiftAssignedAsync(Shift shift, Guid requesterUserId, CancellationToken cancellationToken)
        {
            if (shift.AssignedTenant.UserId is not { } recipient || recipient == requesterUserId)
            {
                return;
            }

            await StoreAsync(new Notification(recipient, NotificationType.ShiftAssigned,
                shift.HouseId, shift.Id, null,
                "Nuovo turno assegnato",
                $"{shift.AssignedRoom.Name}: dal {FormatDate(shift.StartDate)} al {FormatDate(shift.EndDate)}.",
                Now), cancellationToken);
        }

        public async Task ShiftHelperChosenAsync(Shift shift, Guid requesterUserId, CancellationToken cancellationToken)
        {
            if (shift.HelperTenant?.UserId is not { } recipient || recipient == requesterUserId)
            {
                return;
            }

            await StoreAsync(new Notification(recipient, NotificationType.ShiftHelperChosen,
                shift.HouseId, shift.Id, null,
                "Ti hanno chiesto una mano",
                $"{shift.AssignedTenant.Name} ti ha scelto come aiuto per il turno {shift.AssignedRoom.Name}: "
                    + $"dal {FormatDate(shift.StartDate)} al {FormatDate(shift.EndDate)}.",
                Now), cancellationToken);
        }

        public async Task ReportCreatedAsync(House house, Shift shift, Report report, CancellationToken cancellationToken)
        {
            var description = Preview(report.Description);

            foreach (var tenant in house.Tenants)
            {
                if (tenant.UserId is not { } recipient || tenant.Id == report.Reporter.Id)
                {
                    continue;
                }

                var isAssignee = tenant.Id == shift.AssignedTenant.Id;
                var title = isAssignee ? "Nuova segnalazione sul tuo turno" : "Nuova segnalazione";
                var body = isAssignee
                    ? $"{report.Reporter.Name} ha segnalato il tuo turno {shift.AssignedRoom.Name}: {description}"
                    : $"{report.Reporter.Name} ha segnalato il turno {shift.AssignedRoom.Name} di {shift.AssignedTenant.Name}: {description}";

                await StoreAsync(new Notification(recipient, NotificationType.ReportCreated,
                    shift.HouseId, shift.Id, report.Id, title, body, Now), cancellationToken);
            }
        }

        // One unread notification per report, refreshed with the latest totals to avoid flooding the reporter
        public async Task ReportVotedAsync(Shift shift, Report report, CancellationToken cancellationToken)
        {
            if (report.Reporter.UserId is not { } recipient)
            {
                return;
            }

            const string title = "Nuovi voti sulla tua segnalazione";
            var body = $"Turno {shift.AssignedRoom.Name}: {report.UpvoteCount} d'accordo, {report.DownvoteCount} in disaccordo.";

            var existing = await notificationRepository.GetUnreadForReportAsync(
                recipient, NotificationType.ReportVoted, report.Id, cancellationToken);
            if (existing is not null)
            {
                existing.Refresh(title, body, Now);
                if (await notificationRepository.TryUpdateAsync(existing, cancellationToken))
                {
                    pushDispatcher.Enqueue(existing.Id);
                }

                return;
            }

            await StoreAsync(new Notification(recipient, NotificationType.ReportVoted,
                shift.HouseId, shift.Id, report.Id, title, body, Now), cancellationToken);
        }

        public async Task<bool> ShiftDueSoonAsync(Shift shift, CancellationToken cancellationToken)
        {
            if (shift.AssignedTenant.UserId is not { } recipient || shift.IsShiftFullyCompleted)
            {
                return false;
            }

            // The unique index is the real guarantee; this check avoids a failing insert on every scan
            if (await notificationRepository.ExistsForShiftAsync(recipient, NotificationType.ShiftDueSoon, shift.Id, cancellationToken))
            {
                return false;
            }

            var remaining = shift.Checklist.Count(item => !item.IsCompleted);
            return await StoreAsync(new Notification(recipient, NotificationType.ShiftDueSoon,
                shift.HouseId, shift.Id, null,
                "Turno in scadenza",
                $"Il turno {shift.AssignedRoom.Name} scade il {FormatDate(shift.EndDate)}: "
                    + (remaining == 1 ? "manca 1 attività." : $"mancano {remaining} attività."),
                Now), cancellationToken);
        }

        // Only notifications actually stored are pushed: the push payload points to the in-app inbox
        private async Task<bool> StoreAsync(Notification notification, CancellationToken cancellationToken)
        {
            if (!await notificationRepository.TryAddAsync(notification, cancellationToken))
            {
                return false;
            }

            pushDispatcher.Enqueue(notification.Id);
            return true;
        }

        private DateTime Now => timeProvider.GetUtcNow().UtcDateTime;

        private static string FormatDate(DateOnly value) => value.ToString("dd/MM", CultureInfo.InvariantCulture);

        private static string Preview(string text) =>
            text.Length <= DescriptionPreviewLength ? text : string.Concat(text.AsSpan(0, DescriptionPreviewLength - 1), "…");
    }
}
