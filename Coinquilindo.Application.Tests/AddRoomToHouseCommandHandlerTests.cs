using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class AddRoomToHouseCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly AddRoomToHouseCommandHandler _handler;

        public AddRoomToHouseCommandHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _handler = new AddRoomToHouseCommandHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_AddRoomWithCleaningTasks_And_CallUpdate_WhenHouseExists()
        {
            // Arrange
            var house = new House("Casa studenti");
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            var command = new AddRoomToHouseCommand(house.Id, "Bagno", ["Pulire il lavandino", "Lavare la doccia"]);

            // Act
            var roomId = await _handler.Handle(command, CancellationToken.None);

            // Assert
            var room = house.Rooms.Should().ContainSingle().Subject;
            room.Id.Should().Be(roomId);
            room.Name.Should().Be("Bagno");
            room.CleaningTasks.Select(c => c.Description)
                .Should().BeEquivalentTo("Pulire il lavandino", "Lavare la doccia");

            await _mockHouseRepository.Received(1).UpdateAsync(house, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_DiscardBlankAndDuplicateCleaningTasks()
        {
            // Arrange
            var house = new House("Casa studenti");
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            var command = new AddRoomToHouseCommand(house.Id, "Cucina",
                ["Lavare i piatti", "", "   ", " lavare i piatti ", "Pulire i fornelli"]);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            house.Rooms.Single().CleaningTasks.Select(c => c.Description)
                .Should().BeEquivalentTo("Lavare i piatti", "Pulire i fornelli");
        }

        [Fact]
        public async Task Handle_Should_ThrowKeyNotFound_WhenHouseDoesNotExist()
        {
            // Arrange
            var command = new AddRoomToHouseCommand(Guid.NewGuid(), "Bagno", []);
            _mockHouseRepository.GetByIdAsync(command.HouseId, Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "House.NotFound");

            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_NotCallUpdate_WhenRoomNameAlreadyExists()
        {
            // Arrange
            var house = new House("Casa studenti");
            house.AddRoom(new Room("Bagno"));
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            var command = new AddRoomToHouseCommand(house.Id, "bagno", []);

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<RoomNameAlreadyExistsException>();
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }
    }
}
