using CoinquiLindo.Application.Auth;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Infrastructure.Auth;
using CoinquiLindo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoinquiLindo.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            // Singleton options let Wolverine build AppDbContext inline instead of using service location
            services.AddDbContext<AppDbContext>(
                options => options.UseSqlite(connectionString),
                optionsLifetime: ServiceLifetime.Singleton);

            services.AddScoped<IHouseRepository, HouseRepository>();
            services.AddScoped<IShiftRepository, ShiftRepository>();
            services.AddScoped<IUserRepository, UserRepository>();

            // JwtOptions are bound by the host (Api) from configuration
            services.TryAddSingleton(TimeProvider.System);
            services.AddSingleton<IAuthTokenService, JwtAuthTokenService>();

            return services;
        }
    }
}
