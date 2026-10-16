using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Repositories;
using MediatR;

namespace CoinquiLindo.Application.Shifts.Commands
{
    public class AssignShiftCommandHandler(IHouseRepository houseRepository, IShiftRepository shiftRepository)
        : IRequestHandler<AssignShiftCommand, Guid>
    {
        public async Task<Guid> Handle(AssignShiftCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId);

            if (house == null)
            {
                throw new KeyNotFoundException("House not found");
            }

            var tenant = house.Tenants.FirstOrDefault(t => t.Id == request.TenantId);

            if(tenant == null)
            {
                throw new InvalidOperationException($"Tenant with ID {request.TenantId} is not found in the specified house.");
            }

            var room = house.Rooms.FirstOrDefault(r => r.Id == request.RoomId);

            if(room == null)
            {
                throw new InvalidOperationException($"Room with ID {request.RoomId} is not found in the specified house.");
            }

            var shift = new Shift(tenant, room, request.StartDate, request.EndDate);

            await shiftRepository.AddAsync(shift, cancellationToken);

            return shift.Id;
        }
    }
}
