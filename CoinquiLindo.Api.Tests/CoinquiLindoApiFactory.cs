using CoinquiLindo.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CoinquiLindo.Api.Tests
{
    public class CoinquiLindoApiFactory : WebApplicationFactory<Program>
    {
        // Kept open for the factory lifetime: the in-memory database is dropped when it closes
        private readonly SqliteConnection _connection;

        public CoinquiLindoApiFactory()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
        }

        public const string JwtSigningKey = "integration-tests-signing-key-32-bytes-min!!";
        public const string MobileCallbackUri = "coinquilindo://auth";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Secrets come from user-secrets in dev: tests provide fake values
            builder.UseSetting("Auth:Jwt:Issuer", "coinquilindo-api");
            builder.UseSetting("Auth:Jwt:Audience", "coinquilindo-mobile");
            builder.UseSetting("Auth:Jwt:SigningKey", JwtSigningKey);
            builder.UseSetting("Auth:Google:ClientId", "test-client-id.apps.googleusercontent.com");
            builder.UseSetting("Auth:Google:ClientSecret", "test-client-secret");
            builder.UseSetting("Auth:MobileCallbackUri", MobileCallbackUri);

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
                services.AddDbContext<AppDbContext>(
                    options => options.UseSqlite(_connection),
                    optionsLifetime: ServiceLifetime.Singleton);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            var host = base.CreateHost(builder);

            using var scope = host.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();

            return host;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}
