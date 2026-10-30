using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Application.Shifts.Queries;
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

            group.MapGet("/", GetHouseShifts)
                .Produces<List<ShiftResponse>>()
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            group.MapPut("/{shiftId:guid}/checklist/{itemId:guid}", CompleteChecklistItem)
                .Produces(StatusCodes.Status204NoContent)
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

        private static async Task<IResult> GetHouseShifts(
            Guid houseId,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var query = new GetHouseShiftsQuery(houseId, user.GetUserId());
            var shifts = await bus.InvokeAsync<List<ShiftResponse>>(query, ct);
            return Results.Ok(shifts);
        }

        private static async Task<IResult> CompleteChecklistItem(
            Guid houseId,
            Guid shiftId,
            Guid itemId,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var command = new CompleteShiftChecklistItemCommand(
                houseId,
                shiftId,
                itemId,
                user.GetUserId());
            await bus.InvokeAsync(command, ct);
            return Results.NoContent();
        }
    }
}
