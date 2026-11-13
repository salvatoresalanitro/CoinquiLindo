namespace CoinquiLindo.Application.Shifts.Commands
{
    public record SetShiftHelperCommand(Guid HouseId, Guid ShiftId, Guid HelperTenantId, Guid RequesterUserId);
}
