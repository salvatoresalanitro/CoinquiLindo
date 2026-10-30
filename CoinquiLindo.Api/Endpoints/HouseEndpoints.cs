using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Api.Hosting;
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

            group.MapGet("/invites/{inviteCode}", GetInvitePreview)
                .RequireRateLimiting(RateLimitingSetup.InvitesPolicy)
                .Produces<InvitePreviewResponse>()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/join", JoinHouse)
                .RequireRateLimiting(RateLimitingSetup.InvitesPolicy)
                .Produces<Guid>()
                .ProducesProblem(StatusCodes.Status404NotFound)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status429TooManyRequests);

            group.MapPost("/{houseId:guid}/invite-code", RegenerateInviteCode)
                .Produces<InviteCodeResponse>()
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

        private static async Task<IResult> GetInvitePreview(string inviteCode, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var query = new GetInvitePreviewQuery(InviteCode: inviteCode, RequesterUserId: user.GetUserId());
            var response = await bus.InvokeAsync<InvitePreviewResponse>(query, ct);

            return Results.Ok(response);
        }

        private static async Task<IResult> JoinHouse([FromBody] JoinHouseRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new JoinHouseCommand(
                InviteCode: request.InviteCode,
                ClaimTenantId: request.ClaimTenantId,
                RequesterUserId: user.GetUserId());
            var houseId = await bus.InvokeAsync<Guid>(command, ct);

            return Results.Ok(houseId);
        }

        private static async Task<IResult> RegenerateInviteCode(Guid houseId, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new RegenerateInviteCodeCommand(HouseId: houseId, RequesterUserId: user.GetUserId());
            var response = await bus.InvokeAsync<InviteCodeResponse>(command, ct);

            return Results.Ok(response);
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