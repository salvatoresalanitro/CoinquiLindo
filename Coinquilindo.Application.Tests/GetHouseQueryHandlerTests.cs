using CoinquiLindo.Application.Houses.Queries;
using CoinquiLindo.Domain.Entities;
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
        public async Task Handle_Should_Return_HouseDto_WhenHouseExists()
        {
            // Arrange
            var house = new House("Casa studenti");
            var tenant = new Tenant("Salvo");
            house.AddTenant(tenant);

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            var query = new GetHouseQuery(house.Id);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(house.Id);
            result.Name.Should().Be(house.Name);
            result.Tenants.Should().ContainSingle(t => t.Name == tenant.Name);
        }

        [Fact]
        public async Task Handle_Should_ReturnNull_WhenHouseDoesNotExist()
        {
            // Arrange
            var query = new GetHouseQuery(Guid.NewGuid());

            _mockHouseRepository.GetByIdAsync(query.HouseId, Arg.Any<CancellationToken>())
                .ReturnsNull();

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }
    }
}
