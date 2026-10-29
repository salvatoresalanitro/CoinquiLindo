using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Users.Queries;
using CoinquiLindo.Shared.Contracts;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class UserEndpoints
    {
        public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/me").WithTags("Users").RequireAuthorization();

            group.MapGet("/", GetMe)
                .Produces<MeResponse>()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status404NotFound);

            return app;
        }

        private static async Task<IResult> GetMe(ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var response = await bus.InvokeAsync<MeResponse>(new GetCurrentUserQuery(UserId: user.GetUserId()), ct);

            return Results.Ok(response);
        }
    }
}
