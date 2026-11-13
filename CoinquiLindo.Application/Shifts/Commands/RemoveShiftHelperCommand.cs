namespace CoinquiLindo.Application.Shifts.Commands
{
    public record RemoveShiftHelperCommand(Guid HouseId, Guid ShiftId, Guid RequesterUserId);
}
