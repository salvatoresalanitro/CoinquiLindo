using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Application.Houses.Queries;
using CoinquiLindo.Shared.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CoinquiLindo.Api.Endpoints
{
    public static class HouseEndpoints
    {
        public static IEndpointRouteBuilder MapHouseEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses").WithTags("Houses");

            group.MapPost("/", CreateHouse);
            group.MapPost("/{houseId:guid}/tenants", AddTenantToHouse);

            group.MapGet("/{houseId:guid}", GetHouse);

            return app;
        }

        private static async Task<IResult> CreateHouse([FromBody] CreateHouseRequest request, ISender sender)
        {
            var command = new CreateHouseCommand(request.Name);
            var id = await sender.Send(command);

            return Results.Ok(id);
        }

        private static async Task<IResult> AddTenantToHouse(Guid houseId, [FromBody] AddTenantRequest request, ISender sender)
        {
            try //TODO: Add middleware for exception handling and logging
            {
                var command = new AddTenantToHouseCommand(houseId, request.TenantName);
                await sender.Send(command);

                return Results.Ok();
            }
            catch(Exception ex) //TODO: Handle specific exceptions and return appropriate status codes
            {
                return Results.BadRequest(ex.Message);
            }
        }

        private static async Task<IResult> GetHouse(Guid houseId, ISender sender)
        {
            var response = await sender.Send(new GetHouseQuery(houseId));

            return response is not null ? Results.Ok(response) : Results.NotFound();
        }
    }
}