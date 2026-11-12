using CoinquiLindo.Application.Shifts.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class CompleteShiftChecklistItemCommandHandlerTests
    {
        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();

        [Fact]
        public async Task Handle_Should_CompleteChecklistItem_When_ShiftBelongsToHouseAndRequesterIsMember()
        {
            var (house, member) = TestData.HouseWithMember("Casa");
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Lavare il pavimento"));
            house.AddRoom(room);
            var shift = new Shift(house.Id, house.Tenants.Single(), room, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            var item = shift.Checklist.Single();
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByIdAsync(house.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            var handler = new CompleteShiftChecklistItemCommandHandler(_houseRepository, _shiftRepository);

            await handler.Handle(
                new CompleteShiftChecklistItemCommand(house.Id, shift.Id, item.Id, member.Id),
                CancellationToken.None);

            item.IsCompleted.Should().BeTrue();
            item.CompletedAt.Should().NotBeNull();
            await _shiftRepository.Received(1).UpdateAsync(shift, Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_NotLoadShift_When_RequesterIsNotHouseMember()
        {
            var (house, _) = TestData.HouseWithMember("Casa");
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            var handler = new CompleteShiftChecklistItemCommandHandler(_houseRepository, _shiftRepository);

            var action = () => handler.Handle(
                new CompleteShiftChecklistItemCommand(house.Id, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive()
                .GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_Should_ThrowNotFound_When_ShiftDoesNotBelongToHouse()
        {
            var (house, member) = TestData.HouseWithMember("Casa");
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByIdAsync(house.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Shift?)null);
            var handler = new CompleteShiftChecklistItemCommandHandler(_houseRepository, _shiftRepository);

            var action = () => handler.Handle(
                new CompleteShiftChecklistItemCommand(house.Id, Guid.NewGuid(), Guid.NewGuid(), member.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>()
                .Where(exception => exception.Code == "Shift.NotFound");
        }
    }
}
