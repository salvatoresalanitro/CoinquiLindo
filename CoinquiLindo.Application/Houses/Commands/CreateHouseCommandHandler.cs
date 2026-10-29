using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
namespace CoinquiLindo.Application.Houses.Commands
{
    public class CreateHouseCommandHandler(IHouseRepository houseRepository, IUserRepository userRepository)
    {
        public async Task<Guid> Handle(CreateHouseCommand request, CancellationToken cancellationToken)
        {
            var creator = await userRepository.GetByIdAsync(request.RequesterUserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.RequesterUserId);

            var house = new House(request.Name);
            house.AddMember(creator);

            await houseRepository.AddAsync(house, cancellationToken);

            return house.Id;
        }
    }
}
