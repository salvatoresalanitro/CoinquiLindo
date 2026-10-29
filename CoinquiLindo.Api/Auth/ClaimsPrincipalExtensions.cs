using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoinquiLindo.Api.Auth
{
    public static class ClaimsPrincipalExtensions
    {
        // Endpoints are protected by RequireAuthorization: a missing/invalid "sub" is a programming error (500)
        public static Guid GetUserId(this ClaimsPrincipal principal) =>
            Guid.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var userId)
                ? userId
                : throw new InvalidOperationException("Authenticated principal has no valid 'sub' claim.");
    }
}
