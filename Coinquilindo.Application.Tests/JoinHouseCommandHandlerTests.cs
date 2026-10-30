using CoinquiLindo.Application.Houses.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Coinquilindo.Application.Tests
{
    public class JoinHouseCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository = Substitute.For<IHouseRepository>();
        private readonly IUserRepository _mockUserRepository = Substitute.For<IUserRepository>();
        private readonly JoinHouseCommandHandler _handler;

        public JoinHouseCommandHandlerTests()
        {
            _handler = new JoinHouseCommandHandler(_mockHouseRepository, _mockUserRepository);
        }

        private (House House, User Joiner) Arrange()
        {
            var (house, _) = TestData.HouseWithMember();
            var joiner = TestData.NewUser("Luca");

            _mockHouseRepository.GetByInviteCodeAsync(house.InviteCode, Arg.Any<CancellationToken>()).Returns(house);
            _mockUserRepository.GetByIdAsync(joiner.Id, Arg.Any<CancellationToken>()).Returns(joiner);

            return (house, joiner);
        }

        [Fact]
        public async Task Handle_Should_AddRequesterAsNewMember_WhenNoTenantIsClaimed()
        {
            // Arrange
            var (house, joiner) = Arrange();
            var command = new JoinHouseCommand(InviteCode: house.InviteCode.ToLowerInvariant(), ClaimTenantId: null, RequesterUserId: joiner.Id);

            // Act
            var houseId = await _handler.Handle(command, CancellationToken.None);

            // Assert
            houseId.Should().Be(house.Id);
            house.IsMember(joiner.Id).Should().BeTrue();
            house.Tenants.Should().HaveCount(2);
            await _mockHouseRepository.Received(1).UpdateAsync(house, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_LinkRequesterToClaimedTenant()
        {
            // Arrange
            var (house, joiner) = Arrange();
            var legacy = new Tenant("Luca");
            house.AddTenant(legacy);
            var command = new JoinHouseCommand(InviteCode: house.InviteCode, ClaimTenantId: legacy.Id, RequesterUserId: joiner.Id);

            // Act
            await _handler.Handle(command, CancellationToken.None);

            // Assert
            legacy.UserId.Should().Be(joiner.Id);
            house.Tenants.Should().HaveCount(2);
            await _mockHouseRepository.Received(1).UpdateAsync(house, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowInviteNotFound_WhenCodeIsUnknown()
        {
            // Arrange
            var (_, joiner) = Arrange();
            var command = new JoinHouseCommand(InviteCode: "ZZZZZZZZ", ClaimTenantId: null, RequesterUserId: joiner.Id);
            _mockHouseRepository.GetByInviteCodeAsync("ZZZZZZZZ", Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Invite.NotFound");
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowInviteNotFound_WhenCodeIsMalformed()
        {
            // Act
            Func<Task> action = async () => await _handler.Handle(
                new JoinHouseCommand(InviteCode: "short", ClaimTenantId: null, RequesterUserId: Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Invite.NotFound");
            await _mockHouseRepository.DidNotReceive().GetByInviteCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowConflict_WhenRequesterIsAlreadyMember()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember();
            _mockHouseRepository.GetByInviteCodeAsync(house.InviteCode, Arg.Any<CancellationToken>()).Returns(house);
            _mockUserRepository.GetByIdAsync(member.Id, Arg.Any<CancellationToken>()).Returns(member);

            // Act
            Func<Task> action = async () => await _handler.Handle(
                new JoinHouseCommand(InviteCode: house.InviteCode, ClaimTenantId: null, RequesterUserId: member.Id), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<UserAlreadyMemberException>();
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowConflict_WhenClaimedTenantAlreadyHasAccount()
        {
            // Arrange
            var (house, joiner) = Arrange();
            var ownerTenant = house.Tenants.Single();

            // Act
            Func<Task> action = async () => await _handler.Handle(
                new JoinHouseCommand(InviteCode: house.InviteCode, ClaimTenantId: ownerTenant.Id, RequesterUserId: joiner.Id), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<TenantAlreadyClaimedException>();
            await _mockHouseRepository.DidNotReceive().UpdateAsync(Arg.Any<House>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenRequesterDoesNotExist()
        {
            // Arrange
            var (house, _) = Arrange();
            var command = new JoinHouseCommand(InviteCode: house.InviteCode, ClaimTenantId: null, RequesterUserId: Guid.NewGuid());
            _mockUserRepository.GetByIdAsync(command.RequesterUserId, Arg.Any<CancellationToken>()).ReturnsNull();

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "User.NotFound");
        }
    }
}
