namespace CoinquiLindo.Application.Rotation.Commands
{
    // Creates the rotation plan of the house or replaces the existing one
    public record SaveRotationPlanCommand(
        Guid HouseId,
        IReadOnlyList<Guid> TenantIds,
        IReadOnlyList<Guid> RoomIds,
        DateOnly StartDate,
        Guid RequesterUserId);
}
