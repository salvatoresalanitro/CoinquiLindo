using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Rotation.Queries
{
    public class GetRotationPlanQueryHandler(
        IHouseRepository houseRepository,
        IRotationPlanRepository rotationPlanRepository,
        TimeProvider timeProvider)
    {
        // Null when the house has no rotation plan
        public async Task<RotationPlanResponse?> Handle(GetRotationPlanQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var plan = await rotationPlanRepository.GetByHouseIdAsync(house.Id, cancellationToken);

            return plan?.ToResponse(house, house.Today(timeProvider));
        }
    }
}
