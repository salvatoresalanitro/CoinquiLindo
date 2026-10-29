using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Users.Queries
{
    public class GetCurrentUserQueryHandler(IUserRepository userRepository, IHouseRepository houseRepository)
    {
        public async Task<MeResponse> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.UserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.UserId);

            var houses = await houseRepository.GetByMemberAsync(user.Id, cancellationToken);

            var housesResponse = houses
                .OrderBy(h => h.Name, StringComparer.OrdinalIgnoreCase)
                .Select(h => new MyHouseResponse(h.Id, h.Name))
                .ToList();

            return new MeResponse(user.Id, user.DisplayName, user.Email, housesResponse);
        }
    }
}
