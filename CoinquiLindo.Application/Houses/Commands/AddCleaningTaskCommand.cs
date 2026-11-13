namespace CoinquiLindo.Application.Houses.Commands
{
    public record AddCleaningTaskCommand(Guid HouseId, Guid RoomId, string Description, Guid RequesterUserId);
}
