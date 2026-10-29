using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
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
        public async Task Handle_Should_AddTenant_And_CallUpdate_WhenRequesterIsMember()
        {
            // Arrange
            var (existingHouse, member) = TestData.HouseWithMember();
            var command = new AddTenantToHouseCommand(HouseId: existingHouse.Id, TenantName: "Luca", RequesterUserId: member.Id);

            _mockHouseRepository.GetByIdAsync(existingHouse.Id, Arg.Any<CancellationToken>()).Returns(existingHouse);

            //Act
            await _handler.Handle(command, CancellationToken.None);

            //Assert
            existingHouse.Tenants.Should().ContainSingle(t => t.Name == "Luca" && t.UserId == null);

            await _mockHouseRepository.Received(1).UpdateAsync(existingHouse, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowException_WhenHouseDoesNotExist()
        {
            // Arrange
            var command = new AddTenantToHouseCommand(HouseId: Guid.NewGuid(), TenantName: "Salvatore", RequesterUserId: Guid.NewGuid());
            _mockHouseRepository.GetByIdAsync(command.HouseId, Arg.Any<CancellationToken>())
                .ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "House.NotFound");

            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowAccessDenied_WhenRequesterIsNotMember()
        {
            // Arrange
            var (house, _) = TestData.HouseWithMember();
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            var command = new AddTenantToHouseCommand(HouseId: house.Id, TenantName: "Intruso", RequesterUserId: Guid.NewGuid());

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotHouseMemberException>();
            house.Tenants.Should().ContainSingle();
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }
    }
}
