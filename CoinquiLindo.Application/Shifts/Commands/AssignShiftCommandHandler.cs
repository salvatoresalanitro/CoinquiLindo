using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Shifts.Commands
{
    public class AssignShiftCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher)
    {
        public async Task<Guid> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var tenant = house.Tenants.FirstOrDefault(t => t.Id == request.TenantId)
                ?? throw new NotFoundException(nameof(Tenant), request.TenantId);

            var room = house.Rooms.FirstOrDefault(r => r.Id == request.RoomId)
                ?? throw new NotFoundException(nameof(Room), request.RoomId);

            var shift = new Shift(house.Id, tenant, room, request.StartDate, request.EndDate, house.GetTimeZone());

            await shiftRepository.AddAsync(shift, cancellationToken);
            await notificationPublisher.ShiftAssignedAsync(shift, request.RequesterUserId, cancellationToken);

            return shift.Id;
        }
    }
}
