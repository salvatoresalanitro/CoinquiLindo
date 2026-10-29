using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Houses.Queries
{
    public class GetHouseQueryHandler(IHouseRepository houseRepository)
    {
        public async Task<HouseResponse?> Handle(GetHouseQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken);

            if (house == null) return null;

            house.EnsureIsMember(request.RequesterUserId);

            var tenantsResponse = house.Tenants
                .Select(t => new TenantResponse(t.Id, t.Name))
                .ToList();

            var roomsResponse = house.Rooms
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .Select(r => new RoomResponse(
                    r.Id,
                    r.Name,
                    r.CleaningTasks
                        .OrderBy(c => c.Description, StringComparer.OrdinalIgnoreCase)
                        .Select(c => new CleaningTaskResponse(c.Id, c.Description))
                        .ToList()))
                .ToList();

            return new HouseResponse(house.Id, house.Name, tenantsResponse, roomsResponse);
        }
    }
}
