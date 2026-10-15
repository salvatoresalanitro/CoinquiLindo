using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReceivedExtensions;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class AddTenantToHouseCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly AddTenantToHouseCommandHandler _handler;

        public AddTenantToHouseCommandHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _handler = new AddTenantToHouseCommandHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_AddTenant_And_CallUpdate_WhenHouseExists()
        {
            // Arrange
            var houseId = Guid.NewGuid();
            var command = new AddTenantToHouseCommand(houseId, "Salvo");
            var existingHouse = new House("Casa studenti");

            _mockHouseRepository.GetByIdAsync(houseId, Arg.Any<CancellationToken>()).Returns(existingHouse);

            //Act
            await _handler.Handle(command, CancellationToken.None);

            //Assert
            existingHouse.Tenants.Should().ContainSingle(t => t.Name == "Salvo");

            await _mockHouseRepository.Received(1).UpdateAsync(existingHouse, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowException_WhenHouseDoesNotExist()
        {
            // Arrange
            var command = new AddTenantToHouseCommand(Guid.NewGuid(), "Salvatore");
            _mockHouseRepository.GetByIdAsync(command.HouseId, Arg.Any<CancellationToken>())
                .ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<KeyNotFoundException>()
                .WithMessage($"House with ID {command.HouseId} not found.");

            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }
    }
}
