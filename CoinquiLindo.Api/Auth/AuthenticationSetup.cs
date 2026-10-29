using System.Text;
using CoinquiLindo.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CoinquiLindo.Api.Auth
{
    public static class AuthenticationSetup
    {
        // Short-lived cookie that only carries the Google identity between /signin-google and /api/auth/google/complete
        public const string ExternalScheme = "External";

        public const string UnauthorizedErrorCode = "Auth.Unauthorized";

        private const string GoogleSectionName = "Auth:Google";

        public static IServiceCollection AddCoinquiLindoAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<JwtOptions>()
                .BindConfiguration(JwtOptions.SectionName)
                .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= JwtOptions.MinSigningKeyBytes,
                    $"{JwtOptions.SectionName}:SigningKey must be at least {JwtOptions.MinSigningKeyBytes} bytes (set it with dotnet user-secrets).")
                .ValidateOnStart();

            services.AddOptions<MobileAuthOptions>().BindConfiguration(MobileAuthOptions.SectionName);

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((options, jwt) =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidIssuer = jwt.Value.Issuer,
                        ValidAudience = jwt.Value.Audience,
                        IssuerSigningKey = jwt.Value.CreateSigningKey(),
                        NameClaimType = "name",
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };
                    options.Events = new JwtBearerEvents { OnChallenge = WriteUnauthorizedProblemAsync };
                });

            var authentication = services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer()
                .AddCookie(ExternalScheme, options =>
                {
                    options.Cookie.Name = "coinquilindo.external";
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
                });

            // Remote schemes are validated on every request by the authentication middleware:
            // register Google only when credentials exist, so the API (and dev-login) work without them
            var google = configuration.GetSection(GoogleSectionName);
            if (!string.IsNullOrWhiteSpace(google["ClientId"]) && !string.IsNullOrWhiteSpace(google["ClientSecret"]))
            {
                authentication.AddGoogle(options =>
                {
                    google.Bind(options);
                    options.SignInScheme = ExternalScheme;
                    // Lax + SameAsRequest: Google redirects back with a top-level GET, and dev runs on plain http
                    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.Events.OnRemoteFailure = RedirectRemoteFailureToAppAsync;
                });
            }

            services.AddAuthorization();

            return services;
        }

        private static async Task WriteUnauthorizedProblemAsync(JwtBearerChallengeContext context)
        {
            // Replace the empty default 401 with a ProblemDetails consistent with GlobalExceptionHandler
            context.HandleResponse();

            var httpContext = context.HttpContext;
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";

            await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Detail = "Authentication is required, or the access token is invalid or expired.",
                    Extensions = { ["errorCode"] = UnauthorizedErrorCode }
                }
            });
        }

        private static Task RedirectRemoteFailureToAppAsync(RemoteFailureContext context)
        {
            // User cancelled consent or correlation failed: hand control back to the app instead of a 500 page
            var callbackUri = context.HttpContext.RequestServices.GetRequiredService<IOptions<MobileAuthOptions>>().Value.MobileCallbackUri;

            context.Response.Redirect($"{callbackUri}#error=sign_in_failed");
            context.HandleResponse();

            return Task.CompletedTask;
        }
    }
}
