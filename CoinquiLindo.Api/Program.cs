using CoinquiLindo.Api.Auth;
using CoinquiLindo.Api.Endpoints;
using CoinquiLindo.Api.ErrorHandling;
using CoinquiLindo.Api.Hosting;
using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Infrastructure;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure("Data Source=coinquilindo.db");
builder.Services.AddCoinquiLindoAuthentication(builder.Configuration);
builder.Services.AddReverseProxySupport(builder.Configuration);
builder.Services.AddCoinquiLindoRateLimiting(builder.Configuration);
builder.Host.UseWolverine(opts =>
    opts.Discovery.IncludeAssembly(typeof(CreateHouseCommand).Assembly));

builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Instance ??= $"{ctx.HttpContext.Request.Method} {ctx.HttpContext.Request.Path}");
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

// Must run first: every later component (OAuth redirect_uri, HTTPS redirection) relies on the original scheme/host
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
// Empty-body error responses (e.g. 404 from Results.NotFound, unknown routes) become ProblemDetails too
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();
// After authentication: invite limits are partitioned per user
app.UseRateLimiter();

// Endpoints mapping
app.MapAuthEndpoints();
app.MapUserEndpoints();
app.MapHouseEndpoints();
app.MapShiftEndpoints();

app.Run();
