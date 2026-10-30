using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class JoinHouseCommandHandler(IHouseRepository houseRepository, IUserRepository userRepository)
    {
        public async Task<Guid> Handle(JoinHouseCommand request, CancellationToken cancellationToken)
        {
            var house = await InviteLookup.FindHouseAsync(houseRepository, request.InviteCode, cancellationToken);

            var user = await userRepository.GetByIdAsync(request.RequesterUserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.RequesterUserId);

            if (request.ClaimTenantId is { } tenantId)
            {
                house.ClaimTenant(tenantId, user);
            }
            else
            {
                house.AddMember(user);
            }

            await houseRepository.UpdateAsync(house, cancellationToken);

            return house.Id;
        }
    }
}
