using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Reports.Queries;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using FluentAssertions;
using NSubstitute;

namespace Coinquilindo.Application.Tests
{
    public class ShiftReportQueryHandlerTests
    {
        private const string PhotoKey = "0f8fad5bd9cb469fa165708167e1f3e1.jpg";

        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();
        private readonly IReportPhotoStorage _photoStorage = Substitute.For<IReportPhotoStorage>();

        [Fact]
        public async Task GetShiftReports_Should_ReturnReportsNewestFirst()
        {
            var (house, member, shift) = ArrangeShiftWithReport();
            var report = shift.Reports.Single();

            var reports = await new GetShiftReportsQueryHandler(_houseRepository, _shiftRepository)
                .Handle(new GetShiftReportsQuery(house.Id, shift.Id, member.Id), CancellationToken.None);

            reports.Should().ContainSingle().Which.Should().BeEquivalentTo(new
            {
                report.Id,
                ShiftId = shift.Id,
                ReporterTenantId = report.Reporter.Id,
                ReporterName = report.Reporter.Name,
                report.Description,
                PhotoUrl = $"/api/houses/{house.Id}/shifts/{shift.Id}/reports/{report.Id}/photo",
                report.CreatedAt
            });
        }

        [Fact]
        public async Task GetShiftReports_Should_CheckMembershipBeforeLoadingShift()
        {
            var (house, _, shift) = ArrangeShiftWithReport();

            var action = () => new GetShiftReportsQueryHandler(_houseRepository, _shiftRepository)
                .Handle(new GetShiftReportsQuery(house.Id, shift.Id, Guid.NewGuid()), CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetShiftReport_Should_ThrowReportNotFound_When_ReportIsNotInShift()
        {
            var (house, member, shift) = ArrangeShiftWithReport();

            var action = () => new GetShiftReportQueryHandler(_houseRepository, _shiftRepository)
                .Handle(new GetShiftReportQuery(house.Id, shift.Id, Guid.NewGuid(), member.Id), CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "Report.NotFound");
        }

        [Fact]
        public async Task GetReportPhoto_Should_ReturnStoredContentWithDetectedContentType()
        {
            var (house, member, shift) = ArrangeShiftWithReport();
            var report = shift.Reports.Single();
            var content = new MemoryStream([0xFF, 0xD8, 0xFF]);
            _photoStorage.OpenReadAsync(PhotoKey, Arg.Any<CancellationToken>()).Returns(content);

            var photo = await new GetReportPhotoQueryHandler(_houseRepository, _shiftRepository, _photoStorage)
                .Handle(new GetReportPhotoQuery(house.Id, shift.Id, report.Id, member.Id), CancellationToken.None);

            photo.Content.Should().BeSameAs(content);
            photo.ContentType.Should().Be("image/jpeg");
        }

        [Fact]
        public async Task GetReportPhoto_Should_NotOpenStorage_When_RequesterIsNotHouseMember()
        {
            var (house, _, shift) = ArrangeShiftWithReport();

            var action = () => new GetReportPhotoQueryHandler(_houseRepository, _shiftRepository, _photoStorage)
                .Handle(new GetReportPhotoQuery(house.Id, shift.Id, shift.Reports.Single().Id, Guid.NewGuid()),
                    CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _photoStorage.DidNotReceive().OpenReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task GetReportPhoto_Should_ThrowNotFound_When_StoredFileIsMissing()
        {
            var (house, member, shift) = ArrangeShiftWithReport();
            _photoStorage.OpenReadAsync(PhotoKey, Arg.Any<CancellationToken>()).Returns((Stream?)null);

            var action = () => new GetReportPhotoQueryHandler(_houseRepository, _shiftRepository, _photoStorage)
                .Handle(new GetReportPhotoQuery(house.Id, shift.Id, shift.Reports.Single().Id, member.Id),
                    CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "ReportPhoto.NotFound");
        }

        private (House House, User Member, Shift Shift) ArrangeShiftWithReport()
        {
            var (house, member) = TestData.HouseWithMember("Casa");
            var assignee = TestData.NewUser("Lucia");
            house.AddMember(assignee);
            var room = new Room("Bagno");
            house.AddRoom(room);
            var shift = new Shift(house.Id, house.Tenants.Single(t => t.UserId == assignee.Id), room,
                DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);
            shift.AddReport(new Report(house.Tenants.Single(t => t.UserId == member.Id), "Pavimento sporco", PhotoKey));
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByIdAsync(house.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            return (house, member, shift);
        }
    }
}
