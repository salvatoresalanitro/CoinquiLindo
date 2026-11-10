using CoinquiLindo.Application.Notifications.Commands;
using Microsoft.Extensions.Options;
using Wolverine;

namespace CoinquiLindo.Api.Hosting
{
    public sealed class NotificationOptions
    {
        public const string SectionName = "Notifications";

        public bool SchedulerEnabled { get; set; } = true;
        // Default: one reminder the day before, only when the checklist is still incomplete
        public TimeSpan ShiftDueSoonLeadTime { get; set; } = TimeSpan.FromHours(24);
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(15);
    }

    public static class NotificationSchedulerSetup
    {
        public static IServiceCollection AddNotificationScheduler(this IServiceCollection services)
        {
            services.AddOptions<NotificationOptions>()
                .BindConfiguration(NotificationOptions.SectionName)
                .Validate(o => o.ShiftDueSoonLeadTime > TimeSpan.Zero, "Notifications:ShiftDueSoonLeadTime must be positive.")
                .Validate(o => o.ScanInterval >= TimeSpan.FromMinutes(1), "Notifications:ScanInterval must be at least 1 minute.")
                .ValidateOnStart();

            services.AddHostedService<ShiftDueSoonScheduler>();
            return services;
        }
    }

    // Single-instance scheduler: duplicate runs are harmless thanks to the unique index on reminders
    public sealed class ShiftDueSoonScheduler(
        IServiceScopeFactory scopeFactory,
        IOptions<NotificationOptions> options,
        TimeProvider timeProvider,
        ILogger<ShiftDueSoonScheduler> logger) : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var settings = options.Value;
            if (!settings.SchedulerEnabled)
            {
                logger.LogInformation("Shift due-soon scheduler disabled by configuration");
                return;
            }

            try
            {
                // Lets the host (and Wolverine) finish starting before the first scan
                await Task.Delay(StartupDelay, timeProvider, stoppingToken);

                using var timer = new PeriodicTimer(settings.ScanInterval, timeProvider);
                do
                {
                    await ScanAsync(settings.ShiftDueSoonLeadTime, stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown
            }
        }

        private async Task ScanAsync(TimeSpan leadTime, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                var created = await bus.InvokeAsync<int>(new CreateShiftDueSoonNotificationsCommand(leadTime), stoppingToken);

                if (created > 0)
                {
                    logger.LogInformation("Created {Count} shift due-soon notifications", created);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed scan must not stop the scheduler: the next tick retries
                logger.LogError(ex, "Shift due-soon scan failed");
            }
        }
    }
}
