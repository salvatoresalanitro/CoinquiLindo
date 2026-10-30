namespace CoinquiLindo.Application.Houses.Commands
{
    public record JoinHouseCommand(string InviteCode, Guid? ClaimTenantId, Guid RequesterUserId);
}
