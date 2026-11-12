using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class ShiftTests
    {
        private static readonly DateOnly Start = new(2026, 11, 2);
        private static readonly DateOnly End = new(2026, 11, 8);

        private static Shift NewShift(Tenant? tenant = null, Room? room = null) =>
            new(Guid.NewGuid(), tenant ?? new Tenant("Salvo"), room ?? new Room("Bagno"), Start, End, TimeZoneInfo.Utc);

        [Fact]
        public void CreateShift_Should_InitializeChecklist_BasedOnRoomTasks()
        {
            //Arrange
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));
            room.AddCleaningTask(new CleaningTask("Pulire il pavimento"));

            //Act
            var shift = NewShift(room: room);

            //Assert
            shift.Checklist.Should().HaveCount(2);
            shift.Checklist.All(c => c.IsCompleted == false).Should().BeTrue();
        }

        [Fact]
        public void MarkItemAsCompleted_Should_SetIsCompletedToTrue()
        {
            //Arrange
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));
            var shift = NewShift(room: room);
            var itemToComplete = shift.Checklist.First();

            //Act
            shift.MarkItemAsCompleted(itemToComplete.Id);

            //Assert
            var completedItem = shift.Checklist.First(c => c.Id == itemToComplete.Id);
            completedItem.IsCompleted.Should().BeTrue();
            completedItem.CompletedAt.Should().NotBeNull();
        }

        [Fact]
        public void AddReport_Should_AddReportToTheShift()
        {
            // Arrange
            var shift = NewShift(tenant: new Tenant("Lucia"));
            var report = new Report(new Tenant("Salvo"), "Manca la carta igienica", "reports/photo.jpg");

            // Act
            shift.AddReport(report);

            // Assert
            shift.Reports.Should().ContainSingle();
            shift.Reports.First().Description.Should().Be("Manca la carta igienica");
        }

        [Fact]
        public void AddReport_Should_Throw_When_ReporterIsTheAssignedTenant()
        {
            // Arrange
            var tenant = new Tenant("Lucia");
            var shift = NewShift(tenant: tenant);
            var report = new Report(tenant, "Auto-segnalazione", "reports/photo.jpg");

            // Act
            var action = () => shift.AddReport(report);

            // Assert
            action.Should().Throw<SelfReportNotAllowedException>()
                .Which.Code.Should().Be("Shift.SelfReportNotAllowed");
            shift.Reports.Should().BeEmpty();
        }

        [Fact]
        public void MarkItemAsCompleted_Should_ThrowNotFound_When_ItemDoesNotExist()
        {
            // Arrange
            var shift = NewShift();
            var missingId = Guid.NewGuid();

            // Act
            var action = () => shift.MarkItemAsCompleted(missingId);

            // Assert
            action.Should().Throw<NotFoundException>()
                .Which.Code.Should().Be("ShiftChecklistItem.NotFound");
        }

        [Fact]
        public void CreateShift_Should_KeepHouseId()
        {
            var houseId = Guid.NewGuid();

            var shift = new Shift(houseId, new Tenant("Salvo"), new Room("Bagno"), Start, End, TimeZoneInfo.Utc);

            shift.HouseId.Should().Be(houseId);
        }

        [Fact]
        public void CreateShift_Should_AllowSingleDayShift()
        {
            var shift = new Shift(Guid.NewGuid(), new Tenant("Salvo"), new Room("Bagno"), Start, Start, TimeZoneInfo.Utc);

            shift.StartDate.Should().Be(Start);
            shift.EndDate.Should().Be(Start);
            shift.DueDate.Should().Be(new DateTime(2026, 11, 3, 0, 0, 0, DateTimeKind.Utc));
        }

        // The deadline is the end of the last day in the house time zone, expressed in UTC
        [Theory]
        [InlineData("UTC", "2026-11-08", "2026-11-09T00:00:00Z")]
        [InlineData("Europe/Rome", "2026-11-08", "2026-11-08T23:00:00Z")]
        [InlineData("Europe/Rome", "2026-07-15", "2026-07-15T22:00:00Z")]
        // Spring forward on 29/03: the day before still ends in CET, the day itself ends in CEST
        [InlineData("Europe/Rome", "2026-03-28", "2026-03-28T23:00:00Z")]
        [InlineData("Europe/Rome", "2026-03-29", "2026-03-29T22:00:00Z")]
        // Fall back on 25/10
        [InlineData("Europe/Rome", "2026-10-24", "2026-10-24T22:00:00Z")]
        [InlineData("Europe/Rome", "2026-10-25", "2026-10-25T23:00:00Z")]
        [InlineData("America/New_York", "2026-11-08", "2026-11-09T05:00:00Z")]
        // Havana switches at midnight: 08/03 00:00 does not exist, 01/11 00:00 happens twice
        [InlineData("America/Havana", "2026-03-07", "2026-03-08T05:00:00Z")]
        [InlineData("America/Havana", "2026-10-31", "2026-11-01T04:00:00Z")]
        public void CreateShift_Should_SetDueDateAtEndOfLastDayInHouseTimeZone(string timeZoneId, string endDate, string expectedDueUtc)
        {
            var end = DateOnly.Parse(endDate);
            var expected = DateTimeOffset.Parse(expectedDueUtc).UtcDateTime;

            var shift = new Shift(Guid.NewGuid(), new Tenant("Salvo"), new Room("Bagno"),
                end.AddDays(-6), end, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));

            shift.DueDate.Should().Be(expected);
            shift.DueDate.Kind.Should().Be(DateTimeKind.Utc);
        }
    }
}
