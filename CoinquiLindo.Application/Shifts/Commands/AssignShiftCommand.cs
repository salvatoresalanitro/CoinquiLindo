namespace CoinquiLindo.Application.Shifts.Commands
{
    public record AssignShiftCommand(
        Guid HouseId,
        Guid RoomId,
        Guid TenantId,
        DateTime StartDate,
        DateTime EndDate
    );
}
