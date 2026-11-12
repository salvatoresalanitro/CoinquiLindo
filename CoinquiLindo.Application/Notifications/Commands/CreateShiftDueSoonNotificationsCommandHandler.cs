using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Notifications.Commands
{
    public class CreateShiftDueSoonNotificationsCommandHandler(
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        public async Task<int> Handle(CreateShiftDueSoonNotificationsCommand request, CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(request.LeadTime, TimeSpan.Zero, nameof(request.LeadTime));

            var now = timeProvider.GetUtcNow().UtcDateTime;
            var shifts = await shiftRepository.GetDueBetweenAsync(now, now.Add(request.LeadTime), cancellationToken);

            var created = 0;
            foreach (var shift in shifts)
            {
                if (await notificationPublisher.ShiftDueSoonAsync(shift, cancellationToken))
                {
                    created++;
                }
            }

            return created;
        }
    }
}
