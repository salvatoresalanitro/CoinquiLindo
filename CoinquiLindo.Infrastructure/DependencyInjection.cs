using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoinquiLindo.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
        {
            services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));

            services.AddScoped<IHouseRepository, HouseRepository>();
            services.AddScoped<IShiftRepository, ShiftRepository>();

            return services;
        }
    }
}
