using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Rotation;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Shifts.Commands
{
    public class SetShiftHelperCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        IRotationPlanRepository rotationPlanRepository,
        NotificationPublisher notificationPublisher)
    {
        public async Task Handle(SetShiftHelperCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var shift = await shiftRepository.GetByIdAsync(house.Id, request.ShiftId, cancellationToken)
                ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

            var helper = house.Tenants.FirstOrDefault(t => t.Id == request.HelperTenantId)
                ?? throw new NotFoundException(nameof(Tenant), request.HelperTenantId);

            // Only a tenant resting that week according to the current plan can help
            var plan = await rotationPlanRepository.GetByHouseIdAsync(house.Id, cancellationToken);
            if (!plan.GetHelperCandidateIds(shift).Contains(helper.Id))
            {
                throw new ShiftHelperNotAllowedException();
            }

            shift.SetHelper(house.GetRequesterTenant(request.RequesterUserId).Id, helper);

            await shiftRepository.UpdateAsync(shift, cancellationToken);
            await notificationPublisher.ShiftHelperChosenAsync(shift, request.RequesterUserId, cancellationToken);
        }
    }
}
