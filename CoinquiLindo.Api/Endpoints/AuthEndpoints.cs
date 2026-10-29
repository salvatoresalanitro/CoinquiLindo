using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Auth.Commands;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class AuthEndpoints
    {
        private const string CompletePath = "/api/auth/google/complete";

        public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

            group.MapGet("/google", StartGoogleSignIn)
                .Produces(StatusCodes.Status302Found);

            group.MapGet("/google/complete", CompleteGoogleSignIn)
                .Produces(StatusCodes.Status302Found);

            group.MapPost("/refresh", Refresh)
                .Produces<AuthResponse>()
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            group.MapPost("/logout", Logout)
                .Produces(StatusCodes.Status204NoContent);

            if (app.ServiceProvider.GetRequiredService<IHostEnvironment>().IsDevelopment())
            {
                group.MapPost("/dev-login", DevLogin)
                    .Produces<AuthResponse>()
                    .ProducesProblem(StatusCodes.Status400BadRequest);
            }

            return app;
        }

        private static async Task<IResult> StartGoogleSignIn(IAuthenticationSchemeProvider schemes, IOptions<MobileAuthOptions> mobileOptions)
        {
            if (await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is null)
            {
                return Results.Redirect($"{mobileOptions.Value.MobileCallbackUri}#error=google_not_configured");
            }

            return Results.Challenge(new AuthenticationProperties { RedirectUri = CompletePath }, [GoogleDefaults.AuthenticationScheme]);
        }

        // The redirect target is fixed by configuration (no client-supplied URL): no open redirect
        private static async Task<IResult> CompleteGoogleSignIn(
            HttpContext httpContext, IOptions<MobileAuthOptions> mobileOptions, IMessageBus bus, CancellationToken ct)
        {
            var callbackUri = mobileOptions.Value.MobileCallbackUri;

            var external = await httpContext.AuthenticateAsync(AuthenticationSetup.ExternalScheme);
            await httpContext.SignOutAsync(AuthenticationSetup.ExternalScheme);

            var subject = external.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = external.Principal?.FindFirstValue(ClaimTypes.Email);

            if (!external.Succeeded || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
            {
                return Results.Redirect($"{callbackUri}#error=sign_in_failed");
            }

            var command = new SignInWithGoogleCommand(
                GoogleSubject: subject,
                Email: email,
                DisplayName: external.Principal!.FindFirstValue(ClaimTypes.Name));

            var auth = await bus.InvokeAsync<AuthResponse>(command, ct);

            // Fragment instead of query string: tokens are never sent back to a server or written in access logs
            var fragment = string.Join('&',
                $"access_token={Uri.EscapeDataString(auth.AccessToken)}",
                $"access_token_expires_at={auth.AccessTokenExpiresAt.ToUnixTimeSeconds()}",
                $"refresh_token={Uri.EscapeDataString(auth.RefreshToken)}",
                $"refresh_token_expires_at={auth.RefreshTokenExpiresAt.ToUnixTimeSeconds()}");

            return Results.Redirect($"{callbackUri}#{fragment}");
        }

        private static async Task<IResult> Refresh([FromBody] RefreshTokenRequest request, IMessageBus bus, CancellationToken ct)
        {
            var auth = await bus.InvokeAsync<AuthResponse>(new RefreshSessionCommand(RefreshToken: request.RefreshToken), ct);

            return Results.Ok(auth);
        }

        private static async Task<IResult> Logout([FromBody] RefreshTokenRequest request, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new SignOutCommand(RefreshToken: request.RefreshToken), ct);

            return Results.NoContent();
        }

        // Development only: lets Windows (no WebAuthenticator) and tests obtain real tokens without Google
        private static async Task<IResult> DevLogin([FromBody] DevLoginRequest request, IMessageBus bus, CancellationToken ct)
        {
            var command = new SignInWithGoogleCommand(
                GoogleSubject: $"dev|{request.Email?.Trim().ToLowerInvariant()}",
                Email: request.Email!,
                DisplayName: request.DisplayName);

            var auth = await bus.InvokeAsync<AuthResponse>(command, ct);

            return Results.Ok(auth);
        }
    }
}
