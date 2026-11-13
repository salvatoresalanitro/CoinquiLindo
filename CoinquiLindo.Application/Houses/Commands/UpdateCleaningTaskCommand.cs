namespace CoinquiLindo.Application.Houses.Commands
{
    public record UpdateCleaningTaskCommand(Guid HouseId, Guid RoomId, Guid TaskId, string Description, Guid RequesterUserId);
}
