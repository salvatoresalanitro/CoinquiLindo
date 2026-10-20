using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
namespace CoinquiLindo.Application.Houses.Commands
{
    public class AddTenantToHouseCommandHandler(IHouseRepository houseRepository)
    {
        public async Task Handle(AddTenantToHouseCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            var newTenant = new Tenant(request.TenantName);

            house.AddTenant(newTenant);

            await houseRepository.UpdateAsync(house, cancellationToken);
        }
    }
}
