using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Users.Commands
{
    public class RegisterPushDeviceCommandHandler(IUserRepository userRepository, TimeProvider timeProvider)
    {
        public async Task Handle(RegisterPushDeviceCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByIdAsync(request.RequesterUserId, cancellationToken)
                ?? throw new NotFoundException(nameof(User), request.RequesterUserId);

            // Validates first: an invalid token must not detach a device from another account
            user.RegisterPushDevice(request.Token!, (PushPlatform)(int)request.Platform, timeProvider.GetUtcNow().UtcDateTime);

            // A physical device follows the account signed in on it (shared phone, account switch)
            var token = request.Token!.Trim();
            var previousOwner = await userRepository.GetByPushTokenAsync(token, cancellationToken);
            if (previousOwner is not null && previousOwner.Id != user.Id)
            {
                previousOwner.RemovePushDevice(token);
            }

            await userRepository.UpdateAsync(user, cancellationToken);
        }
    }
}
