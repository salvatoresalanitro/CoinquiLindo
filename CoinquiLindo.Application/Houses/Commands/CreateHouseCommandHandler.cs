using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Repositories;
using MediatR;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class CreateHouseCommandHandler(IHouseRepository houseRepository) : IRequestHandler<CreateHouseCommand, Guid>
    {
        public async Task<Guid> Handle(CreateHouseCommand request, CancellationToken cancellationToken)
        {
            var house = new House(request.Name);

            await houseRepository.AddAsync(house, cancellationToken);

            return house.Id;
        }
    }
}
