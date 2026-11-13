using CoinquiLindo.Application.Rotation.Commands;
using Microsoft.Extensions.Options;
using Wolverine;

namespace CoinquiLindo.Api.Hosting
{
    public sealed class RotationOptions
    {
        public const string SectionName = "Rotation";

        public bool SchedulerEnabled { get; set; } = true;
        // Shifts of the next week appear within this interval after a week starts
        public TimeSpan ScanInterval { get; set; } = TimeSpan.FromHours(1);
    }

    public static class RotationSchedulerSetup
    {
        public static IServiceCollection AddRotationScheduler(this IServiceCollection services)
        {
            services.AddOptions<RotationOptions>()
                .BindConfiguration(RotationOptions.SectionName)
                .Validate(o => o.ScanInterval >= TimeSpan.FromMinutes(1), "Rotation:ScanInterval must be at least 1 minute.")
                .ValidateOnStart();

            services.AddHostedService<RotationScheduler>();
            return services;
        }
    }

    // Single-instance scheduler: duplicate runs are harmless thanks to the unique index on rotation shifts
    public sealed class RotationScheduler(
        IServiceScopeFactory scopeFactory,
        IOptions<RotationOptions> options,
        TimeProvider timeProvider,
        ILogger<RotationScheduler> logger) : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var settings = options.Value;
            if (!settings.SchedulerEnabled)
            {
                logger.LogInformation("Rotation scheduler disabled by configuration");
                return;
            }

            try
            {
                // Lets the host (and Wolverine) finish starting before the first run
                await Task.Delay(StartupDelay, timeProvider, stoppingToken);

                using var timer = new PeriodicTimer(settings.ScanInterval, timeProvider);
                do
                {
                    await GenerateAsync(stoppingToken);
                }
                while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown
            }
        }

        private async Task GenerateAsync(CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
                var created = await bus.InvokeAsync<int>(new GenerateRotationShiftsCommand(), stoppingToken);

                if (created > 0)
                {
                    logger.LogInformation("Generated {Count} rotation shifts", created);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // A failed run must not stop the scheduler: the next tick retries
                logger.LogError(ex, "Rotation shift generation failed");
            }
        }
    }
}
