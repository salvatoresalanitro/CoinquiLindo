namespace CoinquiLindo.Application.Shifts.Commands
{
    public record AssignShiftCommand(
        Guid HouseId,
        Guid RoomId,
        Guid TenantId,
        DateOnly StartDate,
        DateOnly EndDate,
        Guid RequesterUserId
    );
}
