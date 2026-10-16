namespace CoinquiLindo.Shared.Contracts
{
    public record AssignShiftRequest(
        Guid TenantId,
        Guid RoomId,
        DateTime StartDate,
        DateTime EndDate
    );
}
