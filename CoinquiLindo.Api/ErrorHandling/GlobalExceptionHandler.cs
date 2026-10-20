using CoinquiLindo.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace CoinquiLindo.Api.ErrorHandling
{
    public sealed class GlobalExceptionHandler(
        IProblemDetailsService problemDetailsService,
        ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
    {
        private const string UnexpectedErrorCode = "General.Unexpected";

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title, errorCode, detail) = exception switch
            {
                DomainValidationException e => (StatusCodes.Status400BadRequest, "Validation failed", e.Code, e.Message),
                NotFoundException e => (StatusCodes.Status404NotFound, "Resource not found", e.Code, e.Message),
                ConflictException e => (StatusCodes.Status409Conflict, "Conflict", e.Code, e.Message),
                BusinessRuleViolationException e => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Code, e.Message),
                DomainException e => (StatusCodes.Status400BadRequest, "Domain error", e.Code, e.Message),
                BadHttpRequestException e => (e.StatusCode, "Bad request", "General.BadRequest", "The request is malformed or contains invalid values."),
                // Never expose internal messages for unexpected failures
                _ => (StatusCodes.Status500InternalServerError, "Unexpected error", UnexpectedErrorCode, "An unexpected error occurred. Please retry later.")
            };

            if (status >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            }
            else
            {
                logger.LogWarning("Request {Method} {Path} failed with {ErrorCode}: {Message}",
                    httpContext.Request.Method, httpContext.Request.Path, errorCode, exception.Message);
            }

            httpContext.Response.StatusCode = status;

            return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = status,
                    Title = title,
                    Detail = detail,
                    Extensions = { ["errorCode"] = errorCode }
                }
            });
        }
    }
}
