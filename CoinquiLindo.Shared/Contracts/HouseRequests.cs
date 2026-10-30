namespace CoinquiLindo.Shared.Contracts
{
    public record CreateHouseRequest(string Name);

    public record AddRoomRequest(string Name, List<string>? CleaningTasks);
}
