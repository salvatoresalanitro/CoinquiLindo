namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class RoomNameAlreadyExistsException(string roomName)
        : ConflictException("House.RoomNameAlreadyExists", $"A room named '{roomName}' already exists in this house.");
}
