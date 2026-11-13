namespace CoinquiLindo.Shared.Contracts
{
    // Order matters: tenant in position i takes the room in position (i + week) mod N
    public record SaveRotationPlanRequest(
        List<Guid> TenantIds,
        List<Guid> RoomIds,
        // First day of week 0, calendar day in the house time zone
        DateOnly StartDate);

    public record RotationRoomResponse(Guid Id, string Name);

    public record RotationAssignmentResponse(Guid TenantId, string TenantName, Guid RoomId, string RoomName);

    public record RotationWeekResponse(
        int Index,
        DateOnly StartDate,
        DateOnly EndDate,
        List<RotationAssignmentResponse> Assignments,
        List<TenantResponse> RestingTenants,
        List<RotationRoomResponse> UnassignedRooms);

    public record RotationPlanResponse(
        Guid Id,
        DateOnly StartDate,
        List<TenantResponse> Tenants,
        List<RotationRoomResponse> Rooms,
        // One full cycle (max 8 weeks) starting from the current week, or from week 0 if the plan has not started yet
        List<RotationWeekResponse> Weeks);
}
