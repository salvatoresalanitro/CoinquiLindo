using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Auth.Commands
{
    public class SignOutCommandHandler(IUserRepository userRepository, IAuthTokenService tokenService, TimeProvider timeProvider)
    {
        // Idempotent: an unknown or already revoked token is not an error for logout
        public async Task Handle(SignOutCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return;
            }

            var tokenHash = tokenService.HashRefreshToken(request.RefreshToken);

            var user = await userRepository.GetByRefreshTokenHashAsync(tokenHash, cancellationToken);
            if (user is null)
            {
                return;
            }

            user.RevokeRefreshToken(tokenHash, timeProvider.GetUtcNow().UtcDateTime);

            await userRepository.UpdateAsync(user, cancellationToken);
        }
    }
}
