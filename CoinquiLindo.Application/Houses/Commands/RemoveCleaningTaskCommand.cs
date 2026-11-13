namespace CoinquiLindo.Application.Houses.Commands
{
    public record RemoveCleaningTaskCommand(Guid HouseId, Guid RoomId, Guid TaskId, Guid RequesterUserId);
}
