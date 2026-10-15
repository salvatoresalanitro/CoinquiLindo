using MediatR;

namespace CoinquiLindo.Application.Houses.Commands
{
    public record AddTenantToHouseCommand(Guid HouseId, string TenantName) : IRequest;
}
