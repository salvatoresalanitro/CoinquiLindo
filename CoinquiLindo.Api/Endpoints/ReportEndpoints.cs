using System.Security.Claims;
using CoinquiLindo.Api.Auth;
using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Reports.Commands;
using CoinquiLindo.Application.Reports.Queries;
using CoinquiLindo.Shared.Contracts;
using Microsoft.AspNetCore.Mvc;
using Wolverine;

namespace CoinquiLindo.Api.Endpoints
{
    public static class ReportEndpoints
    {
        // Photo limit plus room for the description and multipart framing
        private const long MaxRequestBytes = ReportForm.MaxPhotoBytes + 1024 * 1024;

        public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("/api/houses/{houseId:guid}/shifts/{shiftId:guid}/reports").WithTags("Reports")
                .RequireAuthorization()
                .ProducesProblem(StatusCodes.Status401Unauthorized)
                .ProducesProblem(StatusCodes.Status403Forbidden)
                .ProducesProblem(StatusCodes.Status404NotFound);

            // Bearer-token API: no cookies to protect, so antiforgery does not apply
            group.MapPost("/", CreateReport)
                .DisableAntiforgery()
                .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
                .WithFormOptions(multipartBodyLengthLimit: MaxRequestBytes)
                .Produces<ReportResponse>(StatusCodes.Status201Created)
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status413PayloadTooLarge)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

            group.MapGet("/", GetReports)
                .Produces<List<ReportResponse>>();

            group.MapGet("/{reportId:guid}", GetReport)
                .Produces<ReportResponse>();

            group.MapGet("/{reportId:guid}/photo", GetReportPhoto)
                .Produces(StatusCodes.Status200OK, responseType: typeof(Stream), contentType: "image/jpeg", "image/png");

            // PUT is idempotent (sets the vote); DELETE removes it. Toggling is a client concern
            group.MapPut("/{reportId:guid}/vote", CastVote)
                .Produces<ReportResponse>()
                .ProducesProblem(StatusCodes.Status400BadRequest)
                .ProducesProblem(StatusCodes.Status409Conflict)
                .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

            group.MapDelete("/{reportId:guid}/vote", RemoveVote)
                .Produces<ReportResponse>()
                .ProducesProblem(StatusCodes.Status409Conflict);

            return app;
        }

        private static async Task<IResult> CastVote(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            CastReportVoteRequest request,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var report = await bus.InvokeAsync<ReportResponse>(
                new CastReportVoteCommand(houseId, shiftId, reportId, request.Vote, user.GetUserId()), ct);
            return Results.Ok(report);
        }

        private static async Task<IResult> RemoveVote(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var report = await bus.InvokeAsync<ReportResponse>(
                new RemoveReportVoteCommand(houseId, shiftId, reportId, user.GetUserId()), ct);
            return Results.Ok(report);
        }

        private static async Task<IResult> CreateReport(
            Guid houseId,
            Guid shiftId,
            [FromForm(Name = ReportForm.DescriptionField)] string? description,
            [FromForm(Name = ReportForm.PhotoField)] IFormFile? photo,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            await using var photoStream = photo?.OpenReadStream();
            var command = new CreateShiftReportCommand(houseId, shiftId, description, photoStream, user.GetUserId());

            var report = await bus.InvokeAsync<ReportResponse>(command, ct);

            return Results.Created($"/api/houses/{houseId}/shifts/{shiftId}/reports/{report.Id}", report);
        }

        private static async Task<IResult> GetReports(
            Guid houseId,
            Guid shiftId,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var reports = await bus.InvokeAsync<List<ReportResponse>>(
                new GetShiftReportsQuery(houseId, shiftId, user.GetUserId()), ct);
            return Results.Ok(reports);
        }

        private static async Task<IResult> GetReport(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            ClaimsPrincipal user,
            IMessageBus bus,
            CancellationToken ct)
        {
            var report = await bus.InvokeAsync<ReportResponse>(
                new GetShiftReportQuery(houseId, shiftId, reportId, user.GetUserId()), ct);
            return Results.Ok(report);
        }

        private static async Task<IResult> GetReportPhoto(
            Guid houseId,
            Guid shiftId,
            Guid reportId,
            ClaimsPrincipal user,
            IMessageBus bus,
            HttpResponse response,
            CancellationToken ct)
        {
            var photo = await bus.InvokeAsync<ReportPhotoContent>(
                new GetReportPhotoQuery(houseId, shiftId, reportId, user.GetUserId()), ct);

            response.Headers.XContentTypeOptions = "nosniff";
            response.Headers.CacheControl = "private, max-age=86400";
            return Results.Stream(photo.Content, photo.ContentType);
        }
    }
}
