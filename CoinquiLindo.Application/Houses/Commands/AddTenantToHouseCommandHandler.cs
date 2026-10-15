using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Repositories;
using MediatR;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class AddTenantToHouseCommandHandler(IHouseRepository houseRepository) : IRequestHandler<AddTenantToHouseCommand>
    {
        public async Task Handle(AddTenantToHouseCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken);

            if(house == null)
            {
                throw new KeyNotFoundException($"House with ID {request.HouseId} not found.");
            }

            var newTenant = new Tenant(request.TenantName);

            house.AddTenant(newTenant);

            await houseRepository.UpdateAsync(house, cancellationToken);
        }
    }
}
