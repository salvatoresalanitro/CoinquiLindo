namespace CoinquiLindo.Shared.Contracts
{
    public record TenantResponse(Guid Id, string Name);
    public record CleaningTaskResponse(Guid Id, string Description);
    public record RoomResponse(Guid Id, string Name, List<CleaningTaskResponse> CleaningTasks);
    public record HouseResponse(Guid Id, string Name, List<TenantResponse> Tenants, List<RoomResponse> Rooms);
}
