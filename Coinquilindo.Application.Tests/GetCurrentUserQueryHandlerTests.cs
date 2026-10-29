using CoinquiLindo.Application.Users.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class GetCurrentUserQueryHandlerTests
    {
        private readonly IUserRepository _mockUserRepository = Substitute.For<IUserRepository>();
        private readonly IHouseRepository _mockHouseRepository = Substitute.For<IHouseRepository>();
        private readonly GetCurrentUserQueryHandler _handler;

        public GetCurrentUserQueryHandlerTests()
        {
            _handler = new GetCurrentUserQueryHandler(_mockUserRepository, _mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_ReturnProfile_WithHousesOrderedByName()
        {
            // Arrange
            var user = new User("google-sub-1", "salvo@gmail.com", "Salvo");
            var milano = new House("Milano");
            var bologna = new House("Bologna");
            milano.AddMember(user);
            bologna.AddMember(user);

            _mockUserRepository.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
            _mockHouseRepository.GetByMemberAsync(user.Id, Arg.Any<CancellationToken>()).Returns([milano, bologna]);

            // Act
            var response = await _handler.Handle(new GetCurrentUserQuery(UserId: user.Id), CancellationToken.None);

            // Assert
            response.Id.Should().Be(user.Id);
            response.DisplayName.Should().Be("Salvo");
            response.Email.Should().Be("salvo@gmail.com");
            response.Houses.Select(h => (h.Id, h.Name)).Should().Equal((bologna.Id, "Bologna"), (milano.Id, "Milano"));
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenUserDoesNotExist()
        {
            // Arrange
            var query = new GetCurrentUserQuery(UserId: Guid.NewGuid());
            _mockUserRepository.GetByIdAsync(query.UserId, Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(query, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "User.NotFound");
        }
    }
}
