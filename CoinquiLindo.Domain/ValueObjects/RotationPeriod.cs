namespace CoinquiLindo.Domain.ValueObjects
{
    public sealed record RotationAssignment(Guid TenantId, Guid RoomId);

    // One week of a rotation plan: who cleans what, who rests and which rooms have no shift
    public sealed record RotationPeriod(
        int Index,
        DateOnly StartDate,
        DateOnly EndDate,
        IReadOnlyList<RotationAssignment> Assignments,
        IReadOnlyList<Guid> RestingTenantIds,
        IReadOnlyList<Guid> UnassignedRoomIds);
}
