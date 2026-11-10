using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoinquiLindo.Infrastructure.Tests
{
    public class NotificationRepositoryTests : IDisposable
    {
        private static readonly DateTime Now = new(2026, 11, 10, 9, 0, 0, DateTimeKind.Utc);

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly DbContextOptions<AppDbContext> _options;

        public NotificationRepositoryTests()
        {
            _connection.Open();
            _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        }

        [Fact]
        public async Task Repository_Should_ReadOwnNotificationsNewestFirst_FilteringAndLimiting()
        {
            var seed = await SeedAsync();
            var older = Create(seed.UserId, seed.HouseId, NotificationType.ShiftAssigned, Now.AddHours(-2));
            var newer = Create(seed.UserId, seed.HouseId, NotificationType.ReportCreated, Now);
            var read = Create(seed.UserId, seed.HouseId, NotificationType.ShiftAssigned, Now.AddHours(-1));
            read.MarkAsRead(Now);
            var foreign = Create(seed.OtherUserId, seed.HouseId, NotificationType.ShiftAssigned, Now);
            await AddAllAsync(older, newer, read, foreign);

            await using var context = CreateContext();
            var repository = CreateRepository(context);

            (await repository.GetForRecipientAsync(seed.UserId, unreadOnly: false, take: 10))
                .Select(n => n.Id).Should().Equal(newer.Id, read.Id, older.Id);
            (await repository.GetForRecipientAsync(seed.UserId, unreadOnly: true, take: 10))
                .Select(n => n.Id).Should().Equal(newer.Id, older.Id);
            (await repository.GetForRecipientAsync(seed.UserId, unreadOnly: false, take: 1))
                .Should().ContainSingle().Which.Id.Should().Be(newer.Id);
            (await repository.CountUnreadAsync(seed.UserId)).Should().Be(2);
            (await repository.GetByIdAsync(seed.UserId, foreign.Id)).Should().BeNull();
            (await repository.GetByIdAsync(seed.OtherUserId, foreign.Id)).Should().NotBeNull();
        }

        [Fact]
        public async Task TryAdd_Should_ReturnFalse_ForDuplicateDueSoonReminder_AndKeepContextUsable()
        {
            var seed = await SeedAsync();
            var shiftId = Guid.NewGuid();

            await using var context = CreateContext();
            var repository = CreateRepository(context);
            (await repository.TryAddAsync(Create(seed.UserId, seed.HouseId, NotificationType.ShiftDueSoon, Now, shiftId))).Should().BeTrue();

            (await repository.TryAddAsync(Create(seed.UserId, seed.HouseId, NotificationType.ShiftDueSoon, Now, shiftId))).Should().BeFalse();
            (await repository.TryAddAsync(Create(seed.UserId, seed.HouseId, NotificationType.ShiftAssigned, Now, shiftId))).Should().BeTrue();

            (await repository.ExistsForShiftAsync(seed.UserId, NotificationType.ShiftDueSoon, shiftId)).Should().BeTrue();
            (await repository.ExistsForShiftAsync(seed.OtherUserId, NotificationType.ShiftDueSoon, shiftId)).Should().BeFalse();
            (await repository.CountUnreadAsync(seed.UserId)).Should().Be(2);
        }

        [Fact]
        public async Task TryAdd_Should_AllowOnlyOneUnreadVoteNotificationPerReport()
        {
            var seed = await SeedAsync();
            var reportId = Guid.NewGuid();
            var first = Create(seed.UserId, seed.HouseId, NotificationType.ReportVoted, Now, reportId: reportId);

            await using (var context = CreateContext())
            {
                var repository = CreateRepository(context);
                (await repository.TryAddAsync(first)).Should().BeTrue();
                (await repository.TryAddAsync(Create(seed.UserId, seed.HouseId, NotificationType.ReportVoted, Now, reportId: reportId)))
                    .Should().BeFalse();
            }

            await using (var context = CreateContext())
            {
                var repository = CreateRepository(context);
                var unread = await repository.GetUnreadForReportAsync(seed.UserId, NotificationType.ReportVoted, reportId);
                unread!.Id.Should().Be(first.Id);
                unread.Refresh("Nuovi voti", "2 d'accordo, 0 in disaccordo", Now.AddMinutes(5));
                (await repository.TryUpdateAsync(unread)).Should().BeTrue();
                unread.MarkAsRead(Now.AddMinutes(10));
                await repository.UpdateAsync(unread);

                (await repository.TryAddAsync(Create(seed.UserId, seed.HouseId, NotificationType.ReportVoted, Now.AddHours(1), reportId: reportId)))
                    .Should().BeTrue("a read notification no longer blocks a new one");
            }

            await using (var context = CreateContext())
            {
                var stored = await context.Set<Notification>().SingleAsync(n => n.Id == first.Id);
                stored.Body.Should().Be("2 d'accordo, 0 in disaccordo");
                stored.ReadAt.Should().Be(Now.AddMinutes(10));
            }
        }

        [Fact]
        public async Task MarkAllAsRead_Should_OnlyTouchUnreadNotificationsOfTheRecipient()
        {
            var seed = await SeedAsync();
            var alreadyRead = Create(seed.UserId, seed.HouseId, NotificationType.ShiftAssigned, Now.AddHours(-1));
            alreadyRead.MarkAsRead(Now.AddMinutes(-30));
            var unread = Create(seed.UserId, seed.HouseId, NotificationType.ReportCreated, Now);
            var foreign = Create(seed.OtherUserId, seed.HouseId, NotificationType.ReportCreated, Now);
            await AddAllAsync(alreadyRead, unread, foreign);

            await using (var context = CreateContext())
            {
                (await CreateRepository(context).MarkAllAsReadAsync(seed.UserId, Now.AddHours(1))).Should().Be(1);
            }

            await using (var context = CreateContext())
            {
                var notifications = await context.Set<Notification>().ToDictionaryAsync(n => n.Id);
                notifications[alreadyRead.Id].ReadAt.Should().Be(Now.AddMinutes(-30));
                notifications[unread.Id].ReadAt.Should().Be(Now.AddHours(1));
                notifications[foreign.Id].ReadAt.Should().BeNull();
            }
        }

        [Fact]
        public async Task GetEndingBetween_Should_ReturnShiftsEndingInWindow_WithAssigneeRoomAndChecklist()
        {
            var house = new House("Casa");
            var assignee = new Tenant("Assegnatario");
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Lavare il pavimento"));
            house.AddTenant(assignee);
            house.AddRoom(room);
            await using (var context = CreateContext())
            {
                await context.Database.MigrateAsync();
                await new HouseRepository(context).AddAsync(house);
            }

            Guid insideId;
            await using (var context = CreateContext())
            {
                var tenant = await context.Tenants.SingleAsync(t => t.Id == assignee.Id);
                var storedRoom = await context.Rooms.Include(r => r.CleaningTasks).SingleAsync(r => r.Id == room.Id);
                var atLowerBound = new Shift(house.Id, tenant, storedRoom, Now.AddDays(-7), Now);
                var inside = new Shift(house.Id, tenant, storedRoom, Now.AddDays(-7), Now.AddHours(24));
                var after = new Shift(house.Id, tenant, storedRoom, Now.AddDays(-7), Now.AddHours(25));
                var repository = new ShiftRepository(context);
                await repository.AddAsync(atLowerBound);
                await repository.AddAsync(inside);
                await repository.AddAsync(after);
                insideId = inside.Id;
            }

            await using (var context = CreateContext())
            {
                var shift = (await new ShiftRepository(context).GetEndingBetweenAsync(Now, Now.AddHours(24)))
                    .Should().ContainSingle().Subject;
                shift.Id.Should().Be(insideId);
                shift.AssignedTenant.Name.Should().Be("Assegnatario");
                shift.AssignedRoom.Name.Should().Be("Bagno");
                shift.Checklist.Should().ContainSingle();
            }
        }

        private static Notification Create(Guid userId, Guid houseId, NotificationType type, DateTime createdAt,
            Guid? shiftId = null, Guid? reportId = null) =>
            new(userId, type, houseId, shiftId, reportId, "Titolo", "Testo", createdAt);

        private async Task AddAllAsync(params Notification[] notifications)
        {
            await using var context = CreateContext();
            var repository = CreateRepository(context);
            foreach (var notification in notifications)
            {
                (await repository.TryAddAsync(notification)).Should().BeTrue();
            }
        }

        private async Task<(Guid HouseId, Guid UserId, Guid OtherUserId)> SeedAsync()
        {
            var user = new User("google-1", "salvo@gmail.com", "Salvo");
            var other = new User("google-2", "lucia@gmail.com", "Lucia");
            var house = new House("Casa");
            house.AddMember(user);
            house.AddMember(other);

            await using var context = CreateContext();
            await context.Database.MigrateAsync();
            context.Users.AddRange(user, other);
            context.Houses.Add(house);
            await context.SaveChangesAsync();
            return (house.Id, user.Id, other.Id);
        }

        private static NotificationRepository CreateRepository(AppDbContext context) =>
            new(context, NullLogger<NotificationRepository>.Instance);

        private AppDbContext CreateContext() => new(_options);

        public void Dispose() => _connection.Dispose();
    }
}
