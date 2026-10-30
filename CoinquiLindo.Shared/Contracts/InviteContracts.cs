namespace CoinquiLindo.Shared.Contracts
{
    // ClaimTenantId: optional existing tenant without account to link to the joining user
    public record JoinHouseRequest(string InviteCode, Guid? ClaimTenantId);

    public record InviteCodeResponse(string InviteCode);

    public record InvitePreviewResponse(Guid HouseId, string HouseName, bool IsAlreadyMember, List<TenantResponse> UnclaimedTenants);
}
