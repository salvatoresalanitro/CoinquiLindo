using CoinquiLindo.Api.Endpoints;
using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Infrastructure;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure("Data Source=coinquilindo.db");
builder.Host.UseWolverine(opts =>
    opts.Discovery.IncludeAssembly(typeof(CreateHouseCommand).Assembly));

var app = builder.Build();

// Endpoints mapping
app.MapHouseEndpoints();
app.MapShiftEndpoints();

app.Run();
