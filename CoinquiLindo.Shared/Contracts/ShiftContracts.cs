namespace CoinquiLindo.Shared.Contracts
{
    public record ShiftChecklistItemResponse(
        Guid Id,
        string Description,
        bool IsCompleted,
        DateTime? CompletedAt);

    public record ShiftResponse(
        Guid Id,
        Guid HouseId,
        Guid AssignedTenantId,
        string AssignedTenantName,
        Guid AssignedRoomId,
        string AssignedRoomName,
        DateOnly StartDate,
        DateOnly EndDate,
        // UTC instant at which the shift expires (end of EndDate in the house time zone)
        DateTime DueDate,
        bool IsShiftFullyCompleted,
        List<ShiftChecklistItemResponse> Checklist,
        List<ReportResponse> Reports,
        // False when the requester is the assignee (self-reports are not allowed)
        bool CanReport);
}
