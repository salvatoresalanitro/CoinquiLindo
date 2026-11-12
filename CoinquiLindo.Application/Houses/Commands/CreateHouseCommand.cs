namespace CoinquiLindo.Application.Houses.Commands
{
    public record CreateHouseCommand(string Name, string? TimeZoneId, Guid RequesterUserId);
}
