using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class RegenerateInviteCodeCommandHandler(IHouseRepository houseRepository)
    {
        public async Task<InviteCodeResponse> Handle(RegenerateInviteCodeCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var inviteCode = house.RegenerateInviteCode();

            await houseRepository.UpdateAsync(house, cancellationToken);

            return new InviteCodeResponse(inviteCode);
        }
    }
}
