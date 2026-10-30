using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses
{
    internal static class InviteLookup
    {
        public const string EntityName = "Invite";

        // Malformed and unknown codes produce the same error: callers cannot tell which codes are close to valid
        public static async Task<House> FindHouseAsync(IHouseRepository houseRepository, string? inviteCode, CancellationToken cancellationToken)
        {
            var normalized = House.NormalizeInviteCode(inviteCode)
                ?? throw new NotFoundException(EntityName, inviteCode ?? string.Empty);

            return await houseRepository.GetByInviteCodeAsync(normalized, cancellationToken)
                ?? throw new NotFoundException(EntityName, normalized);
        }
    }
}
