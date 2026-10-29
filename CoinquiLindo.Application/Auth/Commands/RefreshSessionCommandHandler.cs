using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Auth.Commands
{
    public class RefreshSessionCommandHandler(IUserRepository userRepository, IAuthTokenService tokenService, TimeProvider timeProvider)
    {
        public async Task<AuthResponse> Handle(RefreshSessionCommand request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                throw new InvalidRefreshTokenException();
            }

            var currentHash = tokenService.HashRefreshToken(request.RefreshToken);

            var user = await userRepository.GetByRefreshTokenHashAsync(currentHash, cancellationToken)
                ?? throw new InvalidRefreshTokenException();

            var response = AuthSessionIssuer.Issue(user, tokenService, timeProvider.GetUtcNow(), rotatedTokenHash: currentHash);

            await userRepository.UpdateAsync(user, cancellationToken);

            return response;
        }
    }
}
