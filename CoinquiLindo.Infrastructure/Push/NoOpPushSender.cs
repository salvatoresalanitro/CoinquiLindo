using CoinquiLindo.Application.Notifications;

namespace CoinquiLindo.Infrastructure.Push
{
    // Used when Firebase is not configured (tests, local dev without credentials): in-app notifications still work
    public sealed class NoOpPushSender : IPushSender
    {
        public Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(new PushSendResult(0, []));
    }
}
