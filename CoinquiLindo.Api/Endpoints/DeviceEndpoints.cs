using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Users.Commands;
using CoinquiLindo.Shared.Contracts;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class DeviceEndpoints
    {
        public static IEndpointRouteBuilder MapDeviceEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/me/devices").WithTags("Devices")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            // Idempotent: called at every app start and whenever FCM rotates the installation ID
            group.MapPut("/", RegisterDevice)
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status400BadRequest);

            group.MapPost("/unregister", UnregisterDevice)
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        private static async Task<IResult> RegisterDevice(
            RegisterPushDeviceRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new RegisterPushDeviceCommand(user.GetUserId(), request.Token, request.Platform), ct);
            return Results.NoContent();
        }

        private static async Task<IResult> UnregisterDevice(
            UnregisterPushDeviceRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new UnregisterPushDeviceCommand(user.GetUserId(), request.Token), ct);
            return Results.NoContent();
        }
    }
}
