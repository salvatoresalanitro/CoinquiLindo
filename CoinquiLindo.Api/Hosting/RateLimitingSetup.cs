using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;

namespace CoinquiLindo.Api.Hosting
{
    public static class RateLimitingSetup
    {
        // Invite codes are bearer secrets: throttling makes guessing them impractical
        public const string InvitesPolicy = "invites";
        public const string TooManyRequestsErrorCode = "General.TooManyRequests";

        public static IServiceCollection AddCoinquiLindoRateLimiting(this IServiceCollection services, IConfiguration configuration)
        {
            var invites = configuration.GetSection("RateLimiting:Invites");
            var permitLimit = invites.GetValue("PermitLimit", 10);
            var window = TimeSpan.FromSeconds(invites.GetValue("WindowSeconds", 60));

            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                // Partition per authenticated user (endpoints require auth); fallback to IP for safety
                options.AddPolicy(InvitesPolicy, httpContext =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        httpContext.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? httpContext.Connection.RemoteIpAddress?.ToString()
                            ?? "anonymous",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = permitLimit,
                            Window = window,
                            QueueLimit = 0
                        }));

                options.OnRejected = WriteTooManyRequestsProblemAsync;
            });

            return services;
        }

        private static async ValueTask WriteTooManyRequestsProblemAsync(OnRejectedContext context, CancellationToken cancellationToken)
        {
            var httpContext = context.HttpContext;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
            }

            httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger(typeof(RateLimitingSetup).FullName!)
                .LogWarning("Rate limit exceeded on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);

            await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too many requests",
                    Detail = "Too many attempts. Please retry later.",
                    Extensions = { ["errorCode"] = TooManyRequestsErrorCode }
                }
            });
        }
    }
}
