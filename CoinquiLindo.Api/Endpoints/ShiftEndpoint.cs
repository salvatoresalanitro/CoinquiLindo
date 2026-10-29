using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class ShiftEndpoint
    {
        public static IEndpointRouteBuilder MapShiftEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses/{houseId:guid}/shifts").WithTags("Shifts")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            group.MapPost("/", AssignShift)
                .Produces<Guid>()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> AssignShift(Guid houseId, [FromBody] AssignShiftRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new AssignShiftCommand(
                HouseId: houseId,
                RoomId: request.RoomId,
                TenantId: request.TenantId,
                StartDate: request.StartDate,
                EndDate: request.EndDate,
                RequesterUserId: user.GetUserId());

            var id = await bus.InvokeAsync<Guid>(command, ct);

            return Results.Ok(id);
        }
    }
}
