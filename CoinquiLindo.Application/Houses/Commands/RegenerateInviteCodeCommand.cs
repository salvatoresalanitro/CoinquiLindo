namespace CoinquiLindo.Application.Houses.Commands
{
    public record RegenerateInviteCodeCommand(Guid HouseId, Guid RequesterUserId);
}
