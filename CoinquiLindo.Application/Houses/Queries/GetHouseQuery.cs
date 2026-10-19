using CoinquiLindo.Shared.Contracts;
using MediatR;

namespace CoinquiLindo.Application.Houses.Queries
{
    public record GetHouseQuery(Guid HouseId) : IRequest<HouseResponse?>;
}
