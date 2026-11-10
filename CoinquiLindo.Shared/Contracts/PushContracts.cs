using System.Text.Json.Serialization;

namespace CoinquiLindo.Shared.Contracts
{
    // Serialized by name; numeric values mirror the domain PushPlatform
    [JsonConverter(typeof(JsonStringEnumConverter<PushPlatformValue>))]
    public enum PushPlatformValue
    {
        Android = 1,
        Ios = 2
    }

    public record RegisterPushDeviceRequest(string? Token, PushPlatformValue Platform);

    // Token in the body, not in the URL: push tokens must not end up in access logs
    public record UnregisterPushDeviceRequest(string? Token);
}
