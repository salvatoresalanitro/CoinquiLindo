using CoinquiLindo.Application.Reports;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses
{
    // Shared steps of the cleaning task handlers (add, rename, remove)
    internal static class CleaningTaskChanges
    {
        public static async Task<(House House, Room Room, Tenant Author)> LoadAsync(
            IHouseRepository houseRepository, Guid houseId, Guid roomId, Guid requesterUserId, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(houseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), houseId);

            house.EnsureIsMember(requesterUserId);

            var room = house.Rooms.FirstOrDefault(r => r.Id == roomId)
                ?? throw new NotFoundException(nameof(Room), roomId);

            return (house, room, house.GetRequesterTenant(requesterUserId));
        }

        // The checklist is a copy made when the shift is created: the shifts of the room not started yet follow the change,
        // the ones in progress or past keep what their assignee saw
        public static async Task UpdateShiftsNotStartedAsync(IShiftRepository shiftRepository, House house, Guid roomId,
            TimeProvider timeProvider, Action<Shift> change, CancellationToken cancellationToken)
        {
            var shifts = await shiftRepository.GetByRoomStartingAfterAsync(house.Id, roomId, house.Today(timeProvider), cancellationToken);

            foreach (var shift in shifts)
            {
                change(shift);
                await shiftRepository.UpdateAsync(shift, cancellationToken);
            }
        }
    }
}
