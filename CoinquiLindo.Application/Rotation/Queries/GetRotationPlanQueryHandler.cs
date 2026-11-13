using CoinquiLindo.Application.Houses;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Rotation.Queries
{
    public class GetRotationPlanQueryHandler(
        IHouseRepository houseRepository,
        IRotationPlanRepository rotationPlanRepository,
        IShiftRepository shiftRepository,
        TimeProvider timeProvider)
    {
        // Null when the house has no rotation plan
        public async Task<RotationPlanResponse?> Handle(GetRotationPlanQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var plan = await rotationPlanRepository.GetByHouseIdAsync(house.Id, cancellationToken);
            if (plan is null)
            {
                return null;
            }

            return await plan.ToResponseAsync(house, house.Today(timeProvider), shiftRepository, cancellationToken);
        }
    }
}
