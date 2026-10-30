namespace CoinquiLindo.Application.Shifts.Commands
{
    public record CompleteShiftChecklistItemCommand(
        Guid HouseId,
        Guid ShiftId,
        Guid ChecklistItemId,
        Guid RequesterUserId);
}
