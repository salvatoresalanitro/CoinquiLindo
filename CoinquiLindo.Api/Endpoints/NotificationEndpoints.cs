using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Notifications.Commands;
using CoinquiLindo.Application.Notifications.Queries;
using CoinquiLindo.Shared.Contracts;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class NotificationEndpoints
    {
        public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/me/notifications").WithTags("Notifications")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized);

            group.MapGet("/", GetNotifications)
                .Produces<List<NotificationResponse>>();

            group.MapGet("/unread-count", GetUnreadCount)
                .Produces<UnreadNotificationsResponse>();

            group.MapPut("/{notificationId:guid}/read", MarkAsRead)
                .Produces(StatusCodes.Status204NoContent)
                .ProducesProblem(StatusCodes.Status404NotFound);

            group.MapPut("/read-all", MarkAllAsRead)
                .Produces(StatusCodes.Status204NoContent);

            return app;
        }

        private static async Task<IResult> GetNotifications(
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct,
            bool unreadOnly = false,
            int take = GetMyNotificationsQueryHandler.MaxPageSize)
        {
            var notifications = await bus.InvokeAsync<List<NotificationResponse>>(
                new GetMyNotificationsQuery(user.GetUserId(), unreadOnly, take), ct);
            return Results.Ok(notifications);
        }

        private static async Task<IResult> GetUnreadCount(ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            var count = await bus.InvokeAsync<UnreadNotificationsResponse>(
                new GetUnreadNotificationsCountQuery(user.GetUserId()), ct);
            return Results.Ok(count);
        }

        private static async Task<IResult> MarkAsRead(Guid notificationId, ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new MarkNotificationAsReadCommand(notificationId, user.GetUserId()), ct);
            return Results.NoContent();
        }

        private static async Task<IResult> MarkAllAsRead(ClaimsPrincipal user, IMessageBus bus, CancellationToken ct)
        {
            await bus.InvokeAsync(new MarkAllNotificationsAsReadCommand(user.GetUserId()), ct);
            return Results.NoContent();
        }
    }
}
