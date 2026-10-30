using CoinquiLindo.Application.Shifts.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class GetHouseShiftsQueryHandlerTests
    {
        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();

        [Fact]
        public async Task Handle_Should_ReturnHouseShiftsWithChecklist_When_RequesterIsMember()
        {
            var (house, member) = TestData.HouseWithMember("Casa");
            var room = new Room("Bagno");
            room.AddCleaningTask(new CleaningTask("Lavare il pavimento"));
            house.AddRoom(room);
            var tenant = house.Tenants.Single();
            var shift = new Shift(house.Id, tenant, room, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByHouseIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(new[] { shift });
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository);

            var result = await handler.Handle(new GetHouseShiftsQuery(house.Id, member.Id), CancellationToken.None);

            result.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                Id = shift.Id,
                HouseId = house.Id,
                AssignedTenantId = tenant.Id,
                AssignedTenantName = tenant.Name,
                AssignedRoomId = room.Id,
                AssignedRoomName = room.Name,
                IsShiftFullyCompleted = false
            });
            result.Single().Checklist.Should().ContainSingle(item =>
                item.Description == "Lavare il pavimento" && !item.IsCompleted && item.CompletedAt == null);
        }

        [Fact]
        public async Task Handle_Should_RejectNonMemberBeforeQueryingShifts()
        {
            var (house, _) = TestData.HouseWithMember("Casa");
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository);

            var action = () => handler.Handle(new GetHouseShiftsQuery(house.Id, Guid.NewGuid()), CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive()
                .GetByHouseIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
