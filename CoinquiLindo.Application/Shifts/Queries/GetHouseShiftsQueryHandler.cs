using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Shifts.Queries
{
    public class GetHouseShiftsQueryHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository)
    {
        public async Task<List<ShiftResponse>> Handle(GetHouseShiftsQuery request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var shifts = await shiftRepository.GetByHouseIdAsync(request.HouseId, cancellationToken);

            return shifts.Select(shift => new ShiftResponse(
                shift.Id,
                shift.HouseId,
                shift.AssignedTenant.Id,
                shift.AssignedTenant.Name,
                shift.AssignedRoom.Id,
                shift.AssignedRoom.Name,
                shift.StartDate,
                shift.EndDate,
                shift.IsShiftFullyCompleted,
                shift.Checklist
                    .OrderBy(item => item.Description, StringComparer.OrdinalIgnoreCase)
                    .Select(item => new ShiftChecklistItemResponse(
                        item.Id,
                        item.Description,
                        item.IsCompleted,
                        item.CompletedAt))
                    .ToList()))
                .ToList();
        }
    }
}
