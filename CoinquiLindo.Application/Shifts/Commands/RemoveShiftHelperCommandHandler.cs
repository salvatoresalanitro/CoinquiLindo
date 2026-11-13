using CoinquiLindo.Application.Reports;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Shifts.Commands
{
    public class RemoveShiftHelperCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task Handle(RemoveShiftHelperCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var shift = await shiftRepository.GetByIdAsync(house.Id, request.ShiftId, cancellationToken)
                ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

            shift.RemoveHelper(house.GetRequesterTenant(request.RequesterUserId).Id);

            await shiftRepository.UpdateAsync(shift, cancellationToken);
        }
    }
}
