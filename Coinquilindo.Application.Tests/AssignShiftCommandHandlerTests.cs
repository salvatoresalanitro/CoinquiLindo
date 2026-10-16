using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Repositories;
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

        [Fact]
        public async Task Handler_Should_CreateAndSaveShift_When_DataIsValid()
        {
            // Arrange
            var house = new House("Stanza studenti");
            var room = new Room("Bagno");
            var tenant = new Tenant("Salvo");

            room.AddCleaningTask(new CleaningTask("Pulire il lavandino"));

            house.AddRoom(room);
            house.AddTenant(tenant);

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(7);
            var command = new AssignShiftCommand(house.Id, room.Id, tenant.Id, startDate, endDate);

            // Act
            var shiftId = await _handler.Handle(command, CancellationToken.None);

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
            var house = new House("Stanza studenti");
            var room = new Room("Bagno");

            house.AddRoom(room);

            _mockHouseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(house);

            var startDate = DateTime.UtcNow;
            var endDate = startDate.AddDays(7);
            var command = new AssignShiftCommand(house.Id, room.Id, Guid.NewGuid(), startDate, endDate);

            // Act
            Func<Task> action = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await action.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage($"Tenant with ID {command.TenantId} is not found in the specified house.");

            await _mockShiftRepository.DidNotReceive().AddAsync(Arg.Any<Shift>(), Arg.Any<CancellationToken>());
        }
    }
}
