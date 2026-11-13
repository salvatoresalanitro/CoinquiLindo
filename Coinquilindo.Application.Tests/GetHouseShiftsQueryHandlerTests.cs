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
            var shift = new Shift(house.Id, tenant, room, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByHouseIdAsync(house.Id, Arg.Any<CancellationToken>())
                .Returns(new[] { shift });
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository, Substitute.For<IRotationPlanRepository>());

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
            result.Single().Reports.Should().BeEmpty();
        }

        [Fact]
        public async Task Handle_Should_IncludeShiftReports_ForHistory()
        {
            var (house, member) = TestData.HouseWithMember("Casa");
            var assignee = TestData.NewUser("Lucia");
            house.AddMember(assignee);
            var room = new Room("Bagno");
            house.AddRoom(room);
            var shift = new Shift(house.Id, house.Tenants.Single(t => t.UserId == assignee.Id), room,
                new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            var report = new Report(house.Tenants.Single(t => t.UserId == member.Id), "Pavimento sporco",
                "0f8fad5bd9cb469fa165708167e1f3e1.png");
            shift.AddReport(report);
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByHouseIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(new[] { shift });
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository, Substitute.For<IRotationPlanRepository>());

            var result = await handler.Handle(new GetHouseShiftsQuery(house.Id, member.Id), CancellationToken.None);

            result.Single().Reports.Should().ContainSingle(response =>
                response.Id == report.Id &&
                response.ReporterName == report.Reporter.Name &&
                response.PhotoUrl == $"/api/houses/{house.Id}/shifts/{shift.Id}/reports/{report.Id}/photo");
        }

        [Fact]
        public async Task Handle_Should_RejectNonMemberBeforeQueryingShifts()
        {
            var (house, _) = TestData.HouseWithMember("Casa");
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            var handler = new GetHouseShiftsQueryHandler(_houseRepository, _shiftRepository, Substitute.For<IRotationPlanRepository>());

            var action = () => handler.Handle(new GetHouseShiftsQuery(house.Id, Guid.NewGuid()), CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive()
                .GetByHouseIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }
    }
}
