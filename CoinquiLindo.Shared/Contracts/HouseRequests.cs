namespace CoinquiLindo.Shared.Contracts
{
    // TimeZoneId: time zone of the device creating the house; the server falls back to its default when missing
    public record CreateHouseRequest(string Name, string? TimeZoneId = null);

    public record AddRoomRequest(string Name, List<string>? CleaningTasks);

    public record AddCleaningTaskRequest(string Description);

    public record UpdateCleaningTaskRequest(string Description);
}
