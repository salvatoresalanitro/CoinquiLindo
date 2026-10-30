namespace CoinquiLindo.Domain.Exceptions
{
    public sealed class TenantAlreadyClaimedException()
        : ConflictException("Tenant.AlreadyClaimed", "This tenant is already linked to an account.");
}
