using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Rotation.Commands;
using CoinquiLindo.Application.Rotation.Queries;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class RotationEndpoints
    {
        public static IEndpointRouteBuilder MapRotationEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses/{houseId:guid}/rotation").WithTags("Rotation")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            group.MapGet("/", GetRotationPlan)
                .Produces<RotationPlanResponse>()
                .Produces(StatusCodes.Status204NoContent);

            group.MapPut("/", SaveRotationPlan)
                .Produces<RotationPlanResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest);

            group.MapDelete("/", DeleteRotationPlan)
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        // 204 when the house has no plan yet
        private static async Task<IResult> GetRotationPlan(Guid houseId, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var plan = await bus.InvokeAsync<RotationPlanResponse?>(new GetRotationPlanQuery(houseId, user.GetUserId()), ct);
            return plan is null ? Results.NoContent() : Results.Ok(plan);
        }

        private static async Task<IResult> SaveRotationPlan(
            Guid houseId, [FromBody] SaveRotationPlanRequest request, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var command = new SaveRotationPlanCommand(
                HouseId: houseId,
                TenantIds: request.TenantIds ?? [],
                RoomIds: request.RoomIds ?? [],
                StartDate: request.StartDate,
                RequesterUserId: user.GetUserId());

            return Results.Ok(await bus.InvokeAsync<RotationPlanResponse>(command, ct));
        }

        private static async Task<IResult> DeleteRotationPlan(Guid houseId, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new DeleteRotationPlanCommand(houseId, user.GetUserId()), ct);
            return Results.NoContent();
        }
    }
}
