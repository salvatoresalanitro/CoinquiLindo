namespace CoinquiLindo.Application.Users.Commands
{
    public record UnregisterPushDeviceCommand(Guid RequesterUserId, string? Token);
}
