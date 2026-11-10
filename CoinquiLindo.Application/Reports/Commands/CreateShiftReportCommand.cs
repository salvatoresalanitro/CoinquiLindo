namespace CoinquiLindo.Application.Reports.Commands
{
    public record CreateShiftReportCommand(
        Guid HouseId,
        Guid ShiftId,
        string? Description,
        Stream? Photo,
        Guid RequesterUserId);
}
