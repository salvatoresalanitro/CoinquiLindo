using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Shifts.Commands
{
    public class CompleteShiftChecklistItemCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task Handle(CompleteShiftChecklistItemCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var shift = await shiftRepository.GetByIdAsync(request.HouseId, request.ShiftId, cancellationToken)
                ?? throw new NotFoundException(nameof(Shift), request.ShiftId);

            shift.MarkItemAsCompleted(request.ChecklistItemId);
            await shiftRepository.UpdateAsync(shift, cancellationToken);
        }
    }
}
