using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class AssignShiftCommandHandlerTests
    {
        private readonly IHouseRepository _mockHouseRepository;
        private readonly IShiftRepository _mockShiftRepository;
        private readonly AssignShiftCommandHandler _handler;

        public AssignShiftCommandHandlerTests()
        {
            _mockHouseRepository = Substitute.For<IHouseRepository>();
            _mockShiftRepository = Substitute.For<IShiftRepository>();
            _handler = new AssignShiftCommandHandler(_mockHouseRepository, _mockShiftRepository);
        }

        private static AssignShiftCommand Command(Guid houseId, Guid roomId, Guid tenantId, Guid requesterUserId) => new(
            HouseId: houseId,
            RoomId: roomId,
            TenantId: tenantId,
            StartDate: DateTime.UtcNow,
            EndDate: DateTime.UtcNow.AddDays(7),
            RequesterUserId: requesterUserId);

        [Fact]
        public async Task Handler_Should_CreateAndSaveShift_When_DataIsValid()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember("Stanza studenti");
            var room = new Room("Bagno");
            var tenant = new Tenant("Luca");

            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));

            house.AddRoom(room);
            house.AddTenant(tenant);

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            // Act
            var shiftId = await _handler.Handle(Command(house.Id, room.Id, tenant.Id, member.Id), CancellationToken.None);

            // Assert
            shiftId.Should().NotBeEmpty();
            await _mockShiftRepository.Received(1).AddAsync(
                Arg.Is<Shift>(s => s.AssignedTenant.Id == tenant.Id && s.AssignedRoom.Id == room.Id),
                Arg.Any<CancellationToken>()
            );
        }

        [Fact]
        public async Task Handle_Should_ThrowException_WhenTenantNotInHouse()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember("Stanza studenti");
            var room = new Room("Bagno");

            house.AddRoom(room);

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            // Act
            Func<Task> action = async () => await _handler.Handle(Command(house.Id, room.Id, Guid.NewGuid(), member.Id), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Tenant.NotFound");

            await _mockShiftRepository.DidNotReceive().AddAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenRoomNotInHouse()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember("Stanza studenti");
            var tenant = house.Tenants.Single();

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            // Act
            Func<Task> action = async () => await _handler.Handle(Command(house.Id, Guid.NewGuid(), tenant.Id, member.Id), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "Room.NotFound");
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_WhenHouseDoesNotExist()
        {
            // Act
            Func<Task> action = async () => await _handler.Handle(
                Command(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotFoundException>()
                .Where(e => e.Code == "House.NotFound");

            await _mockShiftRepository.DidNotReceive().AddAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowAccessDenied_WhenRequesterIsNotMember()
        {
            // Arrange
            var (house, member) = TestData.HouseWithMember("Stanza studenti");
            var room = new Room("Bagno");
            house.AddRoom(room);
            var tenant = house.Tenants.Single();

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            // Act
            Func<Task> action = async () => await _handler.Handle(Command(house.Id, room.Id, tenant.Id, Guid.NewGuid()), CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _mockShiftRepository.DidNotReceive().AddAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }
    }
}
