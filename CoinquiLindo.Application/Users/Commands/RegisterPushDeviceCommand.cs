using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Users.Commands
{
    public record RegisterPushDeviceCommand(Guid RequesterUserId, string? Token, PushPlatformValue Platform);
}
