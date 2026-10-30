using CoinquiLindo.Application.Houses.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class GetInvitePreviewQueryHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository = Substitute.For<IHouseRepository>();
        private readonly GetInvitePreviewQueryHandler _handler;

        public GetInvitePreviewQueryHandlerTests()
        {
            _handler = new GetInvitePreviewQueryHandler(_mockHouseRepository);
        }

        [Fact]
        public async Task Handle_Should_ReturnHouseNameAndUnclaimedTenantsOrderedByName()
        {
            // Arrange
            var (house, _) = TestData.HouseWithMember("Casa Milano");
            var marco = new Tenant("Marco");
            var anna = new Tenant("Anna");
            house.AddTenant(marco);
            house.AddTenant(anna);
            _mockHouseRepository.GetByInviteCodeAsync(house.InviteCode, Arg.Any<CancellationToken>()).Returns(house);

            var query = new GetInvitePreviewQuery(InviteCode: FormatForUser(house.InviteCode), RequesterUserId: Guid.NewGuid());

            // Act
            var response = await _handler.Handle(query, CancellationToken.None);

            // Assert
            response.HouseId.Should().Be(house.Id);
            response.HouseName.Should().Be("Casa Milano");
            response.IsAlreadyMember.Should().BeFalse();
            response.UnclaimedTenants.Select(t => (t.Id, t.Name)).Should().Equal((anna.Id, "Anna"), (marco.Id, "Marco"));
        }

        [Fact]
        public async Task Handle_Should_FlagAlreadyMember()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember();
            _mockHouseRepository.GetByInviteCodeAsync(house.InviteCode, Arg.Any<CancellationToken>()).Returns(house);

            // Act
            var response = await _handler.Handle(new GetInvitePreviewQuery(InviteCode: house.InviteCode, RequesterUserId: member.Id), CancellationToken.None);

            // Assert
            response.IsAlreadyMember.Should().BeTrue();
            response.UnclaimedTenants.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_Should_ThrowInviteNotFound_WhenCodeDoesNotMatchAnyHouse()
        {
            // Arrange
            _mockHouseRepository.GetByInviteCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(new GetInvitePreviewQuery(InviteCode: "ABCDEFGH", RequesterUserId: Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Invite.NotFound");
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-code")]
        [InlineData("ABCDEFG0")]
        public async Task Handle_Should_ThrowInviteNotFound_WithoutQuerying_WhenCodeIsMalformed(string code)
        {
            // Act
            Func<Task> action = async () => await _handler.Handle(new GetInvitePreviewQuery(InviteCode: code, RequesterUserId: Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Invite.NotFound");
            await _mockHouseRepository.DidNotReceive().GetByInviteCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        // Codes are typed by users as "abcd-efgh"
        private static string FormatForUser(string code) => $"{code[..4]}-{code[4..]}".ToLowerInvariant();
    }
}
