namespace CoinquiLindo.Application.Houses.Commands
{
    public record CreateHouseCommand(string Name, Guid RequesterUserId);
}
