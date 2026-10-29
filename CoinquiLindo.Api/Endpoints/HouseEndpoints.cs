using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Application.Houses.Queries;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class HouseEndpoints
    {
        public static IEndpointRouteBuilder MapHouseEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses").WithTags("Houses")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            group.MapPost("/", CreateHouse)
                .Produces<Guid>()
                .ProducesProblem(StatusCodes.Status400BadRequest);

            group.MapPost("/{houseId:guid}/tenants", AddTenantToHouse)
                .Produces(StatusCodes.Status200OK)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            group.MapPost("/{houseId:guid}/rooms", AddRoomToHouse)
                .Produces<Guid>()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict);

            group.MapGet("/{houseId:guid}", GetHouse)
                .Produces<HouseResponse>()
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> CreateHouse([FromBody] CreateHouseRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new CreateHouseCommand(Name: request.Name, RequesterUserId: user.GetUserId());
            var id = await bus.InvokeAsync<Guid>(command, ct);

            return Results.Ok(id);
        }

        private static async Task<IResult> AddTenantToHouse(Guid houseId, [FromBody] AddTenantRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new AddTenantToHouseCommand(HouseId: houseId, TenantName: request.TenantName, RequesterUserId: user.GetUserId());
            await bus.InvokeAsync(command, ct);

            return Results.Ok();
        }

        private static async Task<IResult> AddRoomToHouse(Guid houseId, [FromBody] AddRoomRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new AddRoomToHouseCommand(
                HouseId: houseId,
                Name: request.Name,
                CleaningTasks: request.CleaningTasks ?? [],
                RequesterUserId: user.GetUserId());
            var roomId = await bus.InvokeAsync<Guid>(command, ct);

            return Results.Ok(roomId);
        }

        private static async Task<IResult> GetHouse(Guid houseId, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var response = await bus.InvokeAsync<HouseResponse?>(new GetHouseQuery(HouseId: houseId, RequesterUserId: user.GetUserId()), ct);

            return response is not null ? Results.Ok(response) : Results.NotFound();
        }
    }
}