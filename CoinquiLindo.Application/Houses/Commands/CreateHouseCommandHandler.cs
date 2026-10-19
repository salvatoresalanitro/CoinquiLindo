using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
namespace CoinquiLindo.Application.Houses.Commands
{
    public class CreateHouseCommandHandler(IHouseRepository houseRepository)
    {
        public async Task<Guid> Handle(CreateHouseCommand request, CancellationToken cancellationToken)
        {
            var house = new House(request.Name);

            await houseRepository.AddAsync(house, cancellationToken);

            return house.Id;
        }
    }
}
