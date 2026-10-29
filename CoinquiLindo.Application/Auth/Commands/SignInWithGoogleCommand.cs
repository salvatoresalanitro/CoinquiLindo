namespace CoinquiLindo.Application.Auth.Commands
{
    public record SignInWithGoogleCommand(string GoogleSubject, string Email, string? DisplayName);
}
