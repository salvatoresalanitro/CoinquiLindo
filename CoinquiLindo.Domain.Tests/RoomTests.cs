using CoinquiLindo.Domain.Entities;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class RoomTests
    {
        [Fact]
        public void AddCleaningTask_Should_AddTaskToTheRoom_WhenaTaskIsValid()
        {
            //Arrange
            var room = new Room("Cucina");
            var task = new CleaningTask("Pulire i fornelli");

            //Act
            room.AddCleaningTask(task);

            //Assert
            room.CleaningTasks.Should().ContainSingle();
            room.CleaningTasks.First().Description.Should().Be("Pulire i fornelli");
        }
    }
}
