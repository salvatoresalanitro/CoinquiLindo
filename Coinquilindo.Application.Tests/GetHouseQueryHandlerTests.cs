using CoinquiLindo.Application.Houses.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class GetHouseQueryHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly GetHouseQueryHandler _handler;

        public GetHouseQueryHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _handler = new GetHouseQueryHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_Return_HouseDto_WhenRequesterIsMember()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember();
            house.AddTenant(new Tenant("Luca"));

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            var query = new GetHouseQuery(HouseId: house.Id, RequesterUserId: member.Id);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(house.Id);
            result.Name.Should().Be(house.Name);
            result.Tenants.Select(t => t.Name).Should().BeEquivalentTo("Salvo", "Luca");
        }

        [Fact]
        public async Task Handle_Should_Return_RoomsWithCleaningTasks_OrderedByName()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember();
            var kitchen = new Room("Cucina");
            kitchen.AddCleaningTask(new CleaningTask("Pulire i fornelli"));
            kitchen.AddCleaningTask(new CleaningTask("Lavare i piatti"));
            house.AddRoom(kitchen);
            house.AddRoom(new Room("Bagno"));

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            // Act
            var result = await _handler.Handle(new GetHouseQuery(HouseId: house.Id, RequesterUserId: member.Id), CancellationToken.None);

            // Assert
            result!.Rooms.Select(r => r.Name).Should().Equal("Bagno", "Cucina");

            var kitchenResponse = result.Rooms.Single(r => r.Id == kitchen.Id);
            kitchenResponse.CleaningTasks.Select(c => c.Description)
                .Should().Equal("Lavare i piatti", "Pulire i fornelli");
        }

        [Fact]
        public async Task Handle_Should_ReturnNull_WhenHouseDoesNotExist()
        {
            // Arrange
            var query = new GetHouseQuery(HouseId: Guid.NewGuid(), RequesterUserId: Guid.NewGuid());

            _mockHouseRepository.GetByIdAsync(query.HouseId, Arg.Any<CancellationToken>())
                .ReturnsNull();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task Handle_Should_ThrowAccessDenied_WhenRequesterIsNotMember()
        {
            // Arrange
            var (house, _) = TestData.HouseWithMember();
            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);

            var query = new GetHouseQuery(HouseId: house.Id, RequesterUserId: Guid.NewGuid());

            // Act
            Func<Task> action = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotHouseMemberException>()
                .Where(e => e.Code == "House.AccessDenied");
        }
    }
}
