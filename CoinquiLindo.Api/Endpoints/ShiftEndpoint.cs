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
            var group = app.MapGroup("/api/houses/{houseId:guid}/shifts").WithTags("Shifts");

            group.MapPost("/", AssignShift);

            return app;
        }

        private static async Task<IResult> AssignShift(Guid houseId, [FromBody] AssignShiftRequest request, IMessageBus bus, CancellationToken ct)
        {
            try //TODO: Add middleware for exception handling and logging
            {
                var command = new AssignShiftCommand(
                    HouseId: houseId,
                    RoomId: request.RoomId,
                    TenantId: request.TenantId,
                    StartDate: request.StartDate,
                    EndDate: request.EndDate);

                var id = await bus.InvokeAsync<Guid>(command, ct);
                return Results.Ok(id);
            }
            catch (Exception ex) //TODO: Handle specific exceptions and return appropriate status codes
            {
                return Results.BadRequest(ex.Message);
            }
        }
    }
}
