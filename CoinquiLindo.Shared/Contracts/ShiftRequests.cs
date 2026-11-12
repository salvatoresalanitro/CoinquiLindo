namespace CoinquiLindo.Shared.Contracts
{
    public record AssignShiftRequest(
        Guid TenantId,
        Guid RoomId,
        // Calendar days in the house time zone, both inclusive
        DateOnly StartDate,
        DateOnly EndDate
    );
}
