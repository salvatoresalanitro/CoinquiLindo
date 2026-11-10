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
        DateTime StartDate,
        DateTime EndDate,
        bool IsShiftFullyCompleted,
        List<ShiftChecklistItemResponse> Checklist,
        List<ReportResponse> Reports,
        // False when the requester is the assignee (self-reports are not allowed)
        bool CanReport);
}
