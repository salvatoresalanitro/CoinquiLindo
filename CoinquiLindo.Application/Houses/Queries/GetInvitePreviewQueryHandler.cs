using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Houses.Queries
{
    // Readable by non-members holding the code: exposes only what is needed to join
    public class GetInvitePreviewQueryHandler(IHouseRepository houseRepository)
    {
        public async Task<InvitePreviewResponse> Handle(GetInvitePreviewQuery request, CancellationToken cancellationToken)
        {
            var house = await InviteLookup.FindHouseAsync(houseRepository, request.InviteCode, cancellationToken);

            var unclaimedTenants = house.Tenants
                .Where(t => t.UserId is null)
                .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
                .Select(t => new TenantResponse(t.Id, t.Name))
                .ToList();

            return new InvitePreviewResponse(house.Id, house.Name, house.IsMember(request.RequesterUserId), unclaimedTenants);
        }
    }
}
