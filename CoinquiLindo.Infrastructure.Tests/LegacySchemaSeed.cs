using CoinquiLindo.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    // Raw SQL seed for migration tests: the current EF model cannot insert into older schemas.
    // Valid from the AddHouseInviteCode migration up to the one before AddShiftDueDateAndHouseTimeZone.
    internal static class LegacySchemaSeed
    {
        public static async Task InsertHouseAsync(AppDbContext context, Guid houseId, Guid roomId, params Guid[] tenantIds)
        {
            var inviteCode = houseId.ToString("N")[..8].ToUpperInvariant();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Houses" ("Id", "Name", "InviteCode") VALUES ({houseId}, {"Casa preesistente"}, {inviteCode});
                """);
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Rooms" ("Id", "Name", "HouseId") VALUES ({roomId}, {"Bagno"}, {houseId});
                """);

            foreach (var tenantId in tenantIds)
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO "Tenants" ("Id", "Name", "HouseId") VALUES ({tenantId}, {"Coinquilino"}, {houseId});
                    """);
            }
        }

        public static Task<string> PreviousMigrationAsync(AppDbContext context, string migrationName)
        {
            var migrations = context.Database.GetMigrations().ToList();
            var index = migrations.FindIndex(migration => migration.EndsWith("_" + migrationName, StringComparison.Ordinal));
            return Task.FromResult(migrations[index - 1]);
        }
    }
}
