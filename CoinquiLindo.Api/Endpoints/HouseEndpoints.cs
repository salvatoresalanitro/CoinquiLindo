using CoinquiLindo.Application.Houses.Commands;
using MediatR;

namespace CoinquiLindo.Api.Endpoints
{
    public static class HouseEndpoints
    {
        public static IEndpointRouteBuilder MapHouseEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses").WithTags("Houses");

            group.MapPost("/", CreateHouse);
            group.MapPost("/{houseId:guid}/tenants", AddTenantToHouse);

            return app;
        }

        private static async Task<IResult> CreateHouse(CreateHouseCommand command, ISender sender)
        {
            var id = await sender.Send(command);
            return Results.Ok(id);
        }

        private static async Task<IResult> AddTenantToHouse(Guid houseId, string tenantName,ISender sender)
        {
            try //TODO: Add middleware for exception handling and logging
            {
                await sender.Send(new AddTenantToHouseCommand(houseId, tenantName)); //TODO: Consider using a DTO for the request body instead of query parameters for better API design
                return Results.Ok();
            }
            catch(Exception ex) //TODO: Handle specific exceptions and return appropriate status codes
            {
                return Results.BadRequest(ex.Message);
            }
        }
    }
}