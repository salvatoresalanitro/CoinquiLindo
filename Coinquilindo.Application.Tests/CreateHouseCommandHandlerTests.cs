using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class CreateHouseCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly IUserRepository _mockUserRepository;
        private readonly CreateHouseCommandHandler _handler;

        public CreateHouseCommandHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _mockUserRepository = Substitute.For<IUserRepository>();
            _handler = new CreateHouseCommandHandler(_mockHouseRepository, _mockUserRepository);
        }

        [Fact]
        public async Task Handle_Should_CreateHouse_WithRequesterAsFirstMember()
        {
            //Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            _mockUserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

            var command = new CreateHouseCommand(Name: "Casa studenti", RequesterUserId: user.Id);

            //Act
            var resultId = await _handler.Handle(command, CancellationToken.None);

            //Assert
            resultId.Should().NotBeEmpty();

            await _mockHouseRepository.Received(1).AddAsync(
                Arg.Is<House>(h => h.Id == resultId
                    && h.Name == "Casa studenti"
                    && h.Tenants.Count == 1
                    && h.Tenants.First().UserId == user.Id
                    && h.Tenants.First().Name == "Salvo"),
                Arg.Any<CancellationToken>()
            );
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenRequesterDoesNotExist()
        {
            //Arrange
            var command = new CreateHouseCommand(Name: "Casa studenti", RequesterUserId: Guid.NewGuid());
            _mockUserRepository.GetByIdAsync(command.RequesterUserId, Arg.Any<CancellationToken>()).ReturnsNull();

            //Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            //Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "User.NotFound");

            await _mockHouseRepository.DidNotReceive().AddAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }
    }
}
