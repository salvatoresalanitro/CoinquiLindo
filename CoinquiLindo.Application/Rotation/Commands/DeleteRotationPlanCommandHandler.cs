using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Rotation.Commands
{
    public class DeleteRotationPlanCommandHandler(
        IHouseRepository houseRepository,
        IRotationPlanRepository rotationPlanRepository,
        RotationShiftGenerator shiftGenerator)
    {
        // Idempotent: nothing to do when the house has no plan
        public async Task Handle(DeleteRotationPlanCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var plan = await rotationPlanRepository.GetByHouseIdAsync(house.Id, cancellationToken);
            if (plan is null)
            {
                return;
            }

            // Past and current shifts stay as history; the ones not started yet would never be cleaned otherwise
            await shiftGenerator.RemoveUnstartedAsync(house, plan, cancellationToken);
            await rotationPlanRepository.RemoveAsync(plan, cancellationToken);
        }
    }
}
