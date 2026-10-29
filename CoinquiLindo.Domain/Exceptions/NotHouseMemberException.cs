namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class NotHouseMemberException()
        : AccessDeniedException("House.AccessDenied", "The current user is not a member of this house.");
}
