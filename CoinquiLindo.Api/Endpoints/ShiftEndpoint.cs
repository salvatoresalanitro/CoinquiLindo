using CoinquiLindo.Application.Shifts.Commands;
using MediatR;

namespace CoinquiLindo.Api.Endpoints
{
    public static class ShiftEndpoint
    {
        public static IEndpointRouteBuilder MapShiftEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/shifts").WithTags("Shifts");

            group.MapPost("/", AssignShift);

            return app;
        }

        private static async Task<IResult> AssignShift(AssignShiftCommand command, ISender sender)
        {
            try //TODO: Add middleware for exception handling and logging
            {
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
