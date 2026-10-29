namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class UserAlreadyMemberException()
        : ConflictException("House.UserAlreadyMember", "The user is already a member of this house.");
}
