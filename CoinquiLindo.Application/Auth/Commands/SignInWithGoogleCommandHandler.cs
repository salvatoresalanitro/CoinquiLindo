using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Shared.Contracts;

namespace CoinquiLindo.Application.Auth.Commands
{
    public class SignInWithGoogleCommandHandler(IUserRepository userRepository, IAuthTokenService tokenService, TimeProvider timeProvider)
    {
        public async Task<AuthResponse> Handle(SignInWithGoogleCommand request, CancellationToken cancellationToken)
        {
            var user = await userRepository.GetByGoogleSubjectAsync(request.GoogleSubject, cancellationToken);
            var isNewUser = user is null;

            if (user is null)
            {
                user = new User(request.GoogleSubject, request.Email, request.DisplayName);
            }
            else
            {
                user.UpdateProfile(request.Email, request.DisplayName);
            }

            var response = AuthSessionIssuer.Issue(user, tokenService, timeProvider.GetUtcNow());

            if (isNewUser)
            {
                await userRepository.AddAsync(user, cancellationToken);
            }
            else
            {
                await userRepository.UpdateAsync(user, cancellationToken);
            }

            return response;
        }
    }
}
