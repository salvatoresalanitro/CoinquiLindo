using MediatR;

namespace CoinquiLindo.Application.Houses.Commands
{
    public record CreateHouseCommand(string Name) : IRequest<Guid>;
}
