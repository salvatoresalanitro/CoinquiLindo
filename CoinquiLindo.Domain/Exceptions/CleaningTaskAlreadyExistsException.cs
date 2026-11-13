namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class CleaningTaskAlreadyExistsException()
        : ConflictException("Room.CleaningTaskAlreadyExists", "The room already has a cleaning task with this description.");
}
