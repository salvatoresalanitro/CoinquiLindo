using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class CreateHouseCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly CreateHouseCommandHandler _handler;

        public CreateHouseCommandHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _handler = new CreateHouseCommandHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_CreateHouse_And_CallRepositorySave()
        {
            //Arrange
            var command = new CreateHouseCommand("Casa studenti");

            //Act
            var resultId = await _handler.Handle(command, CancellationToken.None);

            //Assert
            resultId.Should().NotBeEmpty();

            await _mockHouseRepository.Received(1).AddAsync(
                Arg.Is<House>(h => h.Name == "Casa studenti"),
                Arg.Any<CancellationToken>()
            );
        }
    }
}
