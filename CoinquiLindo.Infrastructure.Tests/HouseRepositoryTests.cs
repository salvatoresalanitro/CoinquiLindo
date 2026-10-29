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

        [Fact]
        public async Task AddAsync_Should_PersistMemberLinkedToUser()
        {
            // Arrange
            var user = await SeedUserAsync("google-sub-1");
            var house = new House("Casa Studenti");
            house.AddMember(user);

            // Act
            await using (var context = CreateContext())
            {
                await new HouseRepository(context).AddAsync(house);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new HouseRepository(assertContext).GetByIdAsync(house.Id);

            persisted!.IsMember(user.Id).Should().BeTrue();
            persisted.Tenants.Should().ContainSingle(t => t.UserId == user.Id && t.Name == user.DisplayName);
        }

        [Fact]
        public async Task GetByMemberAsync_Should_ReturnOnlyHousesWhereUserIsMember()
        {
            // Arrange
            var user = await SeedUserAsync("google-sub-1");
            var other = await SeedUserAsync("google-sub-2");

            var mine = new House("Casa mia");
            mine.AddMember(user);
            var shared = new House("Casa condivisa");
            shared.AddMember(other);
            shared.AddMember(user);
            var notMine = new House("Casa altrui");
            notMine.AddMember(other);
            notMine.AddTenant(new Tenant("Omonimo senza account"));

            await using (var seedContext = CreateContext())
            {
                var seedRepository = new HouseRepository(seedContext);
                await seedRepository.AddAsync(mine);
                await seedRepository.AddAsync(shared);
                await seedRepository.AddAsync(notMine);
            }

            // Act
            await using var context = CreateContext();
            var houses = await new HouseRepository(context).GetByMemberAsync(user.Id);

            // Assert
            houses.Select(h => h.Id).Should().BeEquivalentTo([mine.Id, shared.Id]);
        }

        private async Task<User> SeedUserAsync(string googleSubject)
        {
            var user = new User(googleSubject, $"{googleSubject}@gmail.com", googleSubject);

            await using var context = CreateContext();
            await new UserRepository(context).AddAsync(user);

            return user;
        }

        public void Dispose() => _connection.Dispose();
    }
}
