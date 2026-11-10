namespace CoinquiLindo.Application.Reports.Queries
{
    public record GetShiftReportsQuery(Guid HouseId, Guid ShiftId, Guid RequesterUserId);
}
