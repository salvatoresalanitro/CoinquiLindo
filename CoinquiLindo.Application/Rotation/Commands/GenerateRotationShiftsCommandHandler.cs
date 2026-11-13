using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Rotation.Commands
{
    public class GenerateRotationShiftsCommandHandler(
        IHouseRepository houseRepository,
        IRotationPlanRepository rotationPlanRepository,
        RotationShiftGenerator shiftGenerator)
    {
        public async Task<int> Handle(GenerateRotationShiftsCommand request, CancellationToken cancellationToken)
        {
            var plans = await rotationPlanRepository.GetAllAsync(cancellationToken);

            var created = 0;
            foreach (var plan in plans)
            {
                var house = await houseRepository.GetByIdAsync(plan.HouseId, cancellationToken);
                if (house is not null)
                {
                    created += await shiftGenerator.GenerateAsync(house, plan, requesterUserId: null, cancellationToken);
                }
            }

            return created;
        }
    }
}
