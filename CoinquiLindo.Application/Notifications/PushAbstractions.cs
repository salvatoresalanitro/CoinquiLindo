namespace CoinquiLindo.Application.Notifications
{
    // Provider-agnostic push payload; Tag lets the device replace an older push for the same notification.
    // OccurredAt (UTC) is the time shown by the device next to the notification
    public sealed record PushMessage(
        IReadOnlyList<string> Tokens,
        string Title,
        string Body,
        string Tag,
        DateTime OccurredAt,
        IReadOnlyDictionary<string, string> Data);

    // InvalidTokens: permanently rejected by the provider (uninstalled app, expired token) and safe to forget
    public sealed record PushSendResult(int SuccessCount, IReadOnlyList<string> InvalidTokens);

    public interface IPushSender
    {
        Task<PushSendResult> SendAsync(PushMessage message, CancellationToken cancellationToken);
    }

    // Queues a stored notification for push delivery outside the request: never blocks or fails the caller
    public interface IPushDispatcher
    {
        void Enqueue(Guid notificationId);
    }
}
