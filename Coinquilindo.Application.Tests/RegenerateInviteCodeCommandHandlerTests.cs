using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class RegenerateInviteCodeCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository = Substitute.For<IHouseRepository>();
        private readonly RegenerateInviteCodeCommandHandler _handler;

        public RegenerateInviteCodeCommandHandlerTests()
        {
            _handler = new RegenerateInviteCodeCommandHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_ReplaceCode_And_Save()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember();
            var oldCode = house.InviteCode;
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            // Act
            var response = await _handler.Handle(new RegenerateInviteCodeCommand(HouseId: house.Id, RequesterUserId: member.Id), CancellationToken.None);

            // Assert
            response.InviteCode.Should().Be(house.InviteCode).And.NotBe(oldCode);
            await _mockHouseRepository.Received(1).UpdateAsync(house, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowAccessDenied_WhenRequesterIsNotMember()
        {
            // Arrange
            var (house, _) = TestData.HouseWithMember();
            var oldCode = house.InviteCode;
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            // Act
            Func<Task> action = async () => await _handler.Handle(
                new RegenerateInviteCodeCommand(HouseId: house.Id, RequesterUserId: Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotHouseMemberException>();
            house.InviteCode.Should().Be(oldCode);
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenHouseDoesNotExist()
        {
            // Arrange
            var command = new RegenerateInviteCodeCommand(HouseId: Guid.NewGuid(), RequesterUserId: Guid.NewGuid());
            _mockHouseRepository.GetByIdAsync(command.HouseId, Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "House.NotFound");
        }
    }
}
