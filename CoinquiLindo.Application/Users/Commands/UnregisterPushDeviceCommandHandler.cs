using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Users.Commands
{
    public class UnregisterPushDeviceCommandHandler(IUserRepository userRepository)
    {
        // Idempotent and scoped to the requester: other accounts' devices cannot be removed
        public async Task Handle(UnregisterPushDeviceCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.RequesterUserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.RequesterUserId);

            if (user.RemovePushDevice(request.Token ?? string.Empty))
            {
                await userRepository.UpdateAsync(user, cancellationToken);
            }
        }
    }
}
