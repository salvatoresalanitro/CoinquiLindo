using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class HouseRepositoryTests : IDisposable
    {
        // The in-memory database lives only as long as this connection stays open
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public HouseRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.Migrate();
        }

        // A fresh context per phase simulates separate HTTP requests and avoids change-tracker false positives
        private AppDbContext CreateContext() => new(_options);

        [Fact]
        public async Task AddAsync_Should_PersistHouseWithChildren()
        {
            // Arrange
            var house = new House("Casa Studenti");
            house.AddTenant(new Tenant("Salvo"));
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));
            house.AddRoom(room);

            // Act
            await using (var context = CreateContext())
            {
                await new HouseRepository(context).AddAsync(house);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new HouseRepository(assertContext).GetByIdAsync(house.Id);

            persisted.Should().NotBeNull();
            persisted!.Tenants.Should().ContainSingle(t => t.Name == "Salvo");
            persisted.Rooms.Should().ContainSingle(r => r.Name == "Bagno")
                .Which.CleaningTasks.Should().ContainSingle(c => c.Description == "Pulire il lavandino");
        }

        [Fact]
        public async Task UpdateAsync_Should_InsertNewTenant_When_HouseIsTracked()
        {
            // Arrange
            var houseId = await SeedHouseAsync();

            // Act
            await using (var context = CreateContext())
            {
                var repository = new HouseRepository(context);
                var house = await repository.GetByIdAsync(houseId);
                house!.AddTenant(new Tenant("Salvo"));

                await repository.UpdateAsync(house);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new HouseRepository(assertContext).GetByIdAsync(houseId);

            persisted!.Tenants.Should().ContainSingle(t => t.Name == "Salvo");
        }

        [Fact]
        public async Task UpdateAsync_Should_InsertNewRoomWithCleaningTasks_When_HouseIsTracked()
        {
            // Arrange
            var houseId = await SeedHouseAsync();

            // Act
            await using (var context = CreateContext())
            {
                var repository = new HouseRepository(context);
                var house = await repository.GetByIdAsync(houseId);
                var room = new Room("Cucina");
                room.AddCleaningTask(new CleaningTask("Lavare i piatti"));
                house!.AddRoom(room);

                await repository.UpdateAsync(house);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new HouseRepository(assertContext).GetByIdAsync(houseId);

            persisted!.Rooms.Should().ContainSingle(r => r.Name == "Cucina")
                .Which.CleaningTasks.Should().ContainSingle(c => c.Description == "Lavare i piatti");
        }

        private async Task<Guid> SeedHouseAsync()
        {
            var house = new House("Casa Studenti");

            await using var context = CreateContext();
            await new HouseRepository(context).AddAsync(house);

            return house.Id;
        }

        public void Dispose() => _connection.Dispose();
    }
}
