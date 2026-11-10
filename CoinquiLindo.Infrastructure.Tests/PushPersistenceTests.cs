using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Infrastructure.Push;
using CoinquiLindo.Infrastructure.Repositories;
using FirebaseAdmin.Messaging;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class PushPersistenceTests : IDisposable
    {
        private const int SqliteConstraintError = 19;
        private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public PushPersistenceTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        }

        [Fact]
        public async Task UserRepository_Should_PersistDevices_AndFindOwnerByToken()
        {
            var user = new User("google-1", "salvo@gmail.com", "Salvo");
            await using (var context = await CreateMigratedContextAsync())
            {
                user.RegisterPushDevice("fcm-token", PushPlatform.Android, Now);
                await new UserRepository(context).AddAsync(user);
            }

            await using (var context = CreateContext())
            {
                var repository = new UserRepository(context);
                var stored = await repository.GetByIdAsync(user.Id);
                stored!.PushDevices.Should().ContainSingle().Which.Should().BeEquivalentTo(new
                {
                    Token = "fcm-token", Platform = PushPlatform.Android, CreatedAt = Now, LastSeenAt = Now
                });
                (await repository.GetByPushTokenAsync("fcm-token"))!.Id.Should().Be(user.Id);
                (await repository.GetByPushTokenAsync("other")).Should().BeNull();

                stored.RemovePushDevice("fcm-token");
                await repository.UpdateAsync(stored);
            }

            await using (var context = CreateContext())
            {
                (await new UserRepository(context).GetByIdAsync(user.Id))!.PushDevices.Should().BeEmpty();
            }
        }

        [Fact]
        public async Task Database_Should_RejectSameTokenOnTwoUsers()
        {
            var first = new User("google-1", "salvo@gmail.com", "Salvo");
            var second = new User("google-2", "lucia@gmail.com", "Lucia");
            first.RegisterPushDevice("fcm-token", PushPlatform.Android, Now);
            second.RegisterPushDevice("fcm-token", PushPlatform.Android, Now);
            await using var context = await CreateMigratedContextAsync();
            await new UserRepository(context).AddAsync(first);

            var action = () => new UserRepository(context).AddAsync(second);

            (await action.Should().ThrowAsync<DbUpdateException>())
                .WithInnerException<SqliteException>().Which.SqliteErrorCode.Should().Be(SqliteConstraintError);
        }

        [Fact]
        public async Task NotificationRepository_FindAsync_Should_IgnoreRecipient()
        {
            var user = new User("google-1", "salvo@gmail.com", "Salvo");
            var house = new House("Casa");
            house.AddMember(user);
            var notification = new Domain.Entities.Notification(user.Id, NotificationType.ShiftAssigned, house.Id,
                Guid.NewGuid(), null, "Titolo", "Testo", Now);
            await using (var context = await CreateMigratedContextAsync())
            {
                context.Users.Add(user);
                context.Houses.Add(house);
                context.Notifications.Add(notification);
                await context.SaveChangesAsync();
            }

            await using var readContext = CreateContext();
            var repository = new NotificationRepository(readContext,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<NotificationRepository>.Instance);
            (await repository.FindAsync(notification.Id))!.RecipientUserId.Should().Be(user.Id);
            (await repository.FindAsync(Guid.NewGuid())).Should().BeNull();
        }

        [Fact]
        public void FcmMessageFactory_Should_MapPushMessageToHighPriorityAndroidMessage()
        {
            var occurredAt = new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc);
            var push = new PushMessage(
                ["token-a", "token-b"],
                "Nuova segnalazione",
                "Marco ha segnalato il turno Bagno",
                "notification-tag",
                occurredAt,
                new Dictionary<string, string> { ["notificationId"] = "n-1", ["unreadCount"] = "3" });

            var message = FcmMessageFactory.Create(push);

            message.Fids.Should().Equal("token-a", "token-b");
            message.Notification.Title.Should().Be("Nuova segnalazione");
            message.Notification.Body.Should().Be("Marco ha segnalato il turno Bagno");
            message.Data.Should().Contain("notificationId", "n-1").And.Contain("unreadCount", "3");
            message.Android.Priority.Should().Be(Priority.High);
            message.Android.Notification.ChannelId.Should().Be(FcmMessageFactory.AndroidChannelId);
            message.Android.Notification.Tag.Should().Be("notification-tag");
            // Unset, FirebaseAdmin sends 0001-01-01 and Android shows the push as thousands of years old
            message.Android.Notification.EventTimestamp.Should().Be(occurredAt);
        }

        [Theory]
        [InlineData(MessagingErrorCode.Unregistered, true)]
        [InlineData(MessagingErrorCode.InvalidArgument, true)]
        [InlineData(MessagingErrorCode.SenderIdMismatch, true)]
        [InlineData(MessagingErrorCode.Unavailable, false)]
        [InlineData(MessagingErrorCode.QuotaExceeded, false)]
        [InlineData(null, false)]
        public void FcmPushSender_Should_DiscardOnlyPermanentlyInvalidTokens(MessagingErrorCode? errorCode, bool isInvalid)
        {
            FcmPushSender.IsPermanentTokenError(errorCode).Should().Be(isInvalid);
        }

        private async Task<AppDbContext> CreateMigratedContextAsync()
        {
            var context = CreateContext();
            await context.Database.MigrateAsync();
            return context;
        }

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
