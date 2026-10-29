namespace CoinquiLindo.Application.Houses.Commands
{
    public record AddRoomToHouseCommand(Guid HouseId, string Name, IReadOnlyList<string> CleaningTasks, Guid RequesterUserId);
}
