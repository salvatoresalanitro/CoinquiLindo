namespace CoinquiLindo.Domain.Enums
{
    // Persisted as int: never renumber (filtered unique indexes reference these values)
    public enum NotificationType
    {
        ShiftAssigned = 1,
        ShiftDueSoon = 2,
        ReportCreated = 3,
        ReportVoted = 4,
        ShiftHelperChosen = 5,
        // One unread notification per recipient and house, refreshed at every change (grouped)
        CleaningTasksChanged = 6
    }
}
