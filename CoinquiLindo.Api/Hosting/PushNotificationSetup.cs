using System.Threading.Channels;
using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Notifications.Commands;
using CoinquiLindo.Infrastructure.Push;
using Wolverine;

namespace CoinquiLindo.Api.Hosting
{
    public static class PushNotificationSetup
    {
        public static IServiceCollection AddPushNotifications(this IServiceCollection services, IConfiguration configuration)
        {
            var credentialsPath = configuration[$"{FirebaseOptions.SectionName}:{nameof(FirebaseOptions.CredentialsPath)}"];
            if (string.IsNullOrWhiteSpace(credentialsPath))
            {
                // Local dev and tests without Firebase: in-app notifications keep working, pushes are skipped
                services.AddSingleton<IPushSender, NoOpPushSender>();
            }
            else
            {
                services.AddOptions<FirebaseOptions>()
                    .BindConfiguration(FirebaseOptions.SectionName)
                    .Validate(o => File.Exists(o.CredentialsPath), "Firebase:CredentialsPath does not point to an existing service account key file.")
                    .ValidateOnStart();
                services.AddSingleton<IPushSender, FcmPushSender>();
            }

            // Type registrations only (no factory lambdas): Wolverine builds handlers without service location
            services.AddSingleton<PushQueue>();
            services.AddSingleton<IPushDispatcher, ChannelPushDispatcher>();
            services.AddHostedService<PushDispatchWorker>();
            return services;
        }
    }

    // In-memory queue: pushes are best effort (the in-app inbox is the source of truth), lost on restart
    public sealed class PushQueue
    {
        private const int Capacity = 1000;

        private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
            new BoundedChannelOptions(Capacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

        public ChannelReader<Guid> Reader => _channel.Reader;

        public bool TryWrite(Guid notificationId) => _channel.Writer.TryWrite(notificationId);
    }

    public sealed class ChannelPushDispatcher(PushQueue queue, ILogger<ChannelPushDispatcher> logger) : IPushDispatcher
    {
        public void Enqueue(Guid notificationId)
        {
            if (!queue.TryWrite(notificationId))
            {
                logger.LogWarning("Push queue full: notification {NotificationId} will not be pushed", notificationId);
            }
        }
    }

    public sealed class PushDispatchWorker(
        PushQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<PushDispatchWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var notificationId in queue.Reader.ReadAllAsync(stoppingToken))
                {
                    await DispatchAsync(notificationId, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Host shutdown
            }
        }

        private async Task DispatchAsync(Guid notificationId, CancellationToken stoppingToken)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                    .InvokeAsync(new SendPushNotificationCommand(notificationId), stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One failed push must not stop the worker
                logger.LogError(ex, "Push dispatch failed for notification {NotificationId}", notificationId);
            }
        }
    }
}
