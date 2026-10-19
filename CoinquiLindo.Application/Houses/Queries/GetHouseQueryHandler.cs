using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;
using MediatR;

namespace CoinquiLindo.Application.Houses.Queries
{
    public class GetHouseQueryHandler(IHouseRepository houseRepository)
    : IRequestHandler<GetHouseQuery, HouseResponse?>
    {
        public async Task<HouseResponse?> Handle(GetHouseQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken);

            if (house == null) return null;

            var tenantsResponse = house.Tenants
                .Select(t => new TenantResponse(t.Id, t.Name))
                .ToList();

            return new HouseResponse(house.Id, house.Name, tenantsResponse);
        }
    }
}
