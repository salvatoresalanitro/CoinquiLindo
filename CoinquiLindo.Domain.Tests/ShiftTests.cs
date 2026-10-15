using CoinquiLindo.Domain.Entities;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class ShiftTests
    {
        [Fact]
        public void CreateShift_Should_InitializeChecklist_BasedOnRoomTasks()
        {
            //Arrange
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));
            room.AddCleaningTask(new CleaningTask("Pulire il pavimento"));

            var tenant = new Tenant("Salvo");
            var stardDate = DateTime.UtcNow.Date;
            var endDate = stardDate.AddDays(7);

            //Act
            var shift = new Shift(tenant, room, stardDate, endDate);

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

            var tenant = new Tenant("Salvo");
            var stardDate = DateTime.UtcNow.Date;
            var endDate = stardDate.AddDays(7);
            var shift = new Shift(tenant, room, stardDate, endDate);
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
            var room = new Room("Bagno");
            var tenant = new Tenant("Lucia");
            var reporter = new Tenant("Salvo");
            var shift = new Shift(tenant, room, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
            var report = new Report(reporter, "Manca la carta igienica", null);

            // Act
            shift.AddReport(report);

            // Assert
            shift.Reports.Should().ContainSingle();
            shift.Reports.First().Description.Should().Be("Manca la carta igienica");
        }
    }
}
