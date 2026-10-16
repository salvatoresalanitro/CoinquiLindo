using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

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

        private static async Task<IResult> AssignShift(Guid houseId, [FromBody] AssignShiftRequest request, ISender sender)
        {
            try //TODO: Add middleware for exception handling and logging
            {
                var command = new AssignShiftCommand(
                houseId,
                request.TenantId,
                request.RoomId,
                request.StartDate,
                request.EndDate);

                var id = await sender.Send(command);
                return Results.Ok(id);
            }
            catch (Exception ex) //TODO: Handle specific exceptions and return appropriate status codes
            {
                return Results.BadRequest(ex.Message);
            }
        }
    }
}
