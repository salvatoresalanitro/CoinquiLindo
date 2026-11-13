using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Rotation.Commands
{
    public class SaveRotationPlanCommandHandler(
        IHouseRepository houseRepository,
        IRotationPlanRepository rotationPlanRepository,
        RotationShiftGenerator shiftGenerator)
    {
        public async Task<RotationPlanResponse> Handle(SaveRotationPlanCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var plan = await rotationPlanRepository.GetByHouseIdAsync(house.Id, cancellationToken);
            if (plan is null)
            {
                plan = new RotationPlan(house, request.TenantIds, request.RoomIds, request.StartDate);
                await rotationPlanRepository.AddAsync(plan, cancellationToken);
            }
            else if (!IsUnchanged(plan, request))
            {
                plan.Update(house, request.TenantIds, request.RoomIds, request.StartDate);
                await rotationPlanRepository.UpdateAsync(plan, cancellationToken);
                // Weeks not started yet follow the new plan
                await shiftGenerator.RemoveUnstartedAsync(house, plan, cancellationToken);
            }

            await shiftGenerator.GenerateAsync(house, plan, request.RequesterUserId, cancellationToken);

            return plan.ToResponse(house, shiftGenerator.Today(house));
        }

        // Saving the same plan again must not rebuild the next weeks (helpers chosen there would be lost)
        private static bool IsUnchanged(RotationPlan plan, SaveRotationPlanCommand request) =>
            plan.StartDate == request.StartDate
            && plan.TenantIds.SequenceEqual(request.TenantIds ?? [])
            && plan.RoomIds.SequenceEqual(request.RoomIds ?? []);
    }
}
