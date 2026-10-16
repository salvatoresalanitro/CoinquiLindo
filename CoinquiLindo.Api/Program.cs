using CoinquiLindo.Api.Endpoints;
using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddInfrastructure("Data Source=coinquilindo.db");
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(CreateHouseCommand).Assembly));

var app = builder.Build();

// Endpoints mapping
app.MapHouseEndpoints();
app.MapShiftEndpoints();

app.Run();
