using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class UserRepositoryTests : IDisposable
    {
        private static readonly DateTime Now = new(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);

        // The in-memory database lives only as long as this connection stays open
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<AppDbContext> _options;

        public UserRepositoryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.Migrate();
        }

        private AppDbContext CreateContext() => new(_options);

        [Fact]
        public async Task AddAsync_Should_PersistUserWithRefreshTokens()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            user.AddRefreshToken("hash-1", Now, Now.AddDays(30));

            // Act
            await using (var context = CreateContext())
            {
                await new UserRepository(context).AddAsync(user);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new UserRepository(assertContext).GetByIdAsync(user.Id);

            persisted.Should().NotBeNull();
            persisted!.GoogleSubject.Should().Be("google-sub-1");
            persisted.Email.Should().Be("salvo@gmail.com");
            persisted.DisplayName.Should().Be("Salvo");
            persisted.RefreshTokens.Should().ContainSingle(t => t.TokenHash == "hash-1" && t.ExpiresAt == Now.AddDays(30));
        }

        [Fact]
        public async Task GetByGoogleSubjectAsync_Should_ReturnMatchingUser_OrNull()
        {
            // Arrange
            var user = await SeedUserAsync("google-sub-1");

            // Act
            await using var context = CreateContext();
            var repository = new UserRepository(context);
            var found = await repository.GetByGoogleSubjectAsync("google-sub-1");
            var missing = await repository.GetByGoogleSubjectAsync("google-sub-unknown");

            // Assert
            found!.Id.Should().Be(user.Id);
            missing.Should().BeNull();
        }

        [Fact]
        public async Task GetByRefreshTokenHashAsync_Should_ReturnOwnerWithTokens()
        {
            // Arrange
            var user = await SeedUserAsync("google-sub-1", "hash-owned");
            await SeedUserAsync("google-sub-2", "hash-other");

            // Act
            await using var context = CreateContext();
            var found = await new UserRepository(context).GetByRefreshTokenHashAsync("hash-owned");

            // Assert
            found!.Id.Should().Be(user.Id);
            found.RefreshTokens.Should().ContainSingle(t => t.TokenHash == "hash-owned");
        }

        [Fact]
        public async Task UpdateAsync_Should_PersistRotation_When_UserIsTracked()
        {
            // Arrange
            var user = await SeedUserAsync("google-sub-1", "old-hash");

            // Act
            await using (var context = CreateContext())
            {
                var repository = new UserRepository(context);
                var tracked = await repository.GetByRefreshTokenHashAsync("old-hash");
                tracked!.RotateRefreshToken("old-hash", "new-hash", Now, Now.AddDays(30));

                await repository.UpdateAsync(tracked);
            }

            // Assert
            await using var assertContext = CreateContext();
            var persisted = await new UserRepository(assertContext).GetByIdAsync(user.Id);

            persisted!.RefreshTokens.Where(t => t.IsActive(Now)).Select(t => t.TokenHash).Should().Equal("new-hash");
        }

        [Fact]
        public async Task AddAsync_Should_Fail_When_GoogleSubjectIsDuplicated()
        {
            // Arrange
            await SeedUserAsync("google-sub-1");

            // Act
            await using var context = CreateContext();
            var action = () => new UserRepository(context).AddAsync(new User("google-sub-1", "other@gmail.com", "Other"));

            // Assert
            await action.Should().ThrowAsync<DbUpdateException>();
        }

        private async Task<User> SeedUserAsync(string googleSubject, string? refreshTokenHash = null)
        {
            var user = new User(googleSubject, $"{googleSubject}@gmail.com", googleSubject);
            if (refreshTokenHash is not null)
            {
                user.AddRefreshToken(refreshTokenHash, Now.AddDays(-1), Now.AddDays(29));
            }

            await using var context = CreateContext();
            await new UserRepository(context).AddAsync(user);

            return user;
        }

        public void Dispose() => _connection.Dispose();
    }
}
