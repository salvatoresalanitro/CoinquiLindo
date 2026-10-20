namespace CoinquiLindo.Client
{
    public sealed record ApiError(int? Status, string? ErrorCode, string UserMessage, string? TraceId)
    {
        public const string NetworkUnavailableCode = "Network.Unavailable";
    }
}
