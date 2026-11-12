using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Application.Reports;
using CoinquiLindo.Application.Reports.Commands;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using CoinquiLindo.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Coinquilindo.Application.Tests
{
    public class CreateShiftReportCommandHandlerTests
    {
        private static readonly byte[] PngPhoto = [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13];

        private readonly IHouseRepository _houseRepository = Substitute.For<IHouseRepository>();
        private readonly IShiftRepository _shiftRepository = Substitute.For<IShiftRepository>();
        private readonly IReportPhotoStorage _photoStorage = Substitute.For<IReportPhotoStorage>();

        [Fact]
        public async Task Handle_Should_StorePhotoAndAddReport_WithRequesterTenantAsReporter()
        {
            var (house, reporter, shift) = ArrangeShiftAssignedToAnotherMember();
            byte[]? storedBytes = null;
            string? storedKey = null;
            _photoStorage.SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>())
                .Returns(call =>
                {
                    storedKey = call.ArgAt<string>(0);
                    using var copy = new MemoryStream();
                    call.ArgAt<Stream>(1).CopyTo(copy);
                    storedBytes = copy.ToArray();
                    return Task.CompletedTask;
                });

            var response = await CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, "Pavimento sporco", new MemoryStream(PngPhoto), reporter.Id),
                CancellationToken.None);

            var report = shift.Reports.Should().ContainSingle().Subject;
            report.Reporter.UserId.Should().Be(reporter.Id);
            report.PhotoKey.Should().MatchRegex("^[0-9a-f]{32}\\.png$").And.Be(storedKey);
            storedBytes.Should().Equal(PngPhoto);
            await _shiftRepository.Received(1).UpdateAsync(shift, Arg.Any<CancellationToken>());
            response.Id.Should().Be(report.Id);
            response.ShiftId.Should().Be(shift.Id);
            response.ReporterTenantId.Should().Be(report.Reporter.Id);
            response.ReporterName.Should().Be(report.Reporter.Name);
            response.Description.Should().Be("Pavimento sporco");
            response.PhotoUrl.Should().Be($"/api/houses/{house.Id}/shifts/{shift.Id}/reports/{report.Id}/photo");
        }

        [Fact]
        public async Task Handle_Should_NotLoadShiftOrStorePhoto_When_RequesterIsNotHouseMember()
        {
            var (house, _, shift) = ArrangeShiftAssignedToAnotherMember();

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, "Pavimento sporco", new MemoryStream(PngPhoto), Guid.NewGuid()),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotHouseMemberException>();
            await _shiftRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
            await AssertNoPhotoStoredAsync();
        }

        [Fact]
        public async Task Handle_Should_ThrowShiftNotFound_When_ShiftIsNotInHouse()
        {
            var (house, reporter, _) = ArrangeShiftAssignedToAnotherMember();

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, Guid.NewGuid(), "Pavimento sporco", new MemoryStream(PngPhoto), reporter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<NotFoundException>().Where(e => e.Code == "Shift.NotFound");
            await AssertNoPhotoStoredAsync();
        }

        [Fact]
        public async Task Handle_Should_RejectSelfReport_WithoutStoringPhoto()
        {
            var (house, _, shift) = ArrangeShiftAssignedToAnotherMember();
            var assigneeUserId = shift.AssignedTenant.UserId!.Value;

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, "Il mio turno", new MemoryStream(PngPhoto), assigneeUserId),
                CancellationToken.None);

            await action.Should().ThrowAsync<SelfReportNotAllowedException>();
            await AssertNoPhotoStoredAsync();
        }

        public static TheoryData<byte[]?, string?, string> InvalidInputs => new()
        {
            { null, "Pavimento sporco", "Report.PhotoRequired" },
            { [], "Pavimento sporco", "Report.PhotoRequired" },
            { "not an image"u8.ToArray(), "Pavimento sporco", "Report.PhotoInvalid" },
            { PngPhoto, null, "Report.DescriptionRequired" },
            { PngPhoto, "   ", "Report.DescriptionRequired" },
        };

        [Theory]
        [MemberData(nameof(InvalidInputs))]
        public async Task Handle_Should_RejectInvalidInput_WithoutStoringPhoto(byte[]? photo, string? description, string expectedCode)
        {
            var (house, reporter, shift) = ArrangeShiftAssignedToAnotherMember();

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, description,
                    photo is null ? null : new MemoryStream(photo), reporter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<DomainValidationException>().Where(e => e.Code == expectedCode);
            shift.Reports.Should().BeEmpty();
            await AssertNoPhotoStoredAsync();
        }

        [Fact]
        public async Task Handle_Should_RejectPhotoAboveLimit_WithoutStoringIt()
        {
            var (house, reporter, shift) = ArrangeShiftAssignedToAnotherMember();
            var oversized = new byte[ReportPhotoFormat.MaxBytes + 1];
            PngPhoto.CopyTo(oversized, 0);

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, "Pavimento sporco", new MemoryStream(oversized), reporter.Id),
                CancellationToken.None);

            await action.Should().ThrowAsync<ReportPhotoTooLargeException>();
            await AssertNoPhotoStoredAsync();
        }

        [Fact]
        public async Task Handle_Should_DeleteStoredPhoto_When_PersistingReportFails()
        {
            var (house, reporter, shift) = ArrangeShiftAssignedToAnotherMember();
            var failure = new InvalidOperationException("database unavailable");
            _shiftRepository.UpdateAsync(shift, Arg.Any<CancellationToken>()).ThrowsAsync(failure);

            var action = () => CreateHandler().Handle(
                new CreateShiftReportCommand(house.Id, shift.Id, "Pavimento sporco", new MemoryStream(PngPhoto), reporter.Id),
                CancellationToken.None);

            (await action.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
            var photoKey = shift.Reports.Single().PhotoKey;
            await _photoStorage.Received(1).SaveAsync(photoKey, Arg.Any<Stream>(), Arg.Any<CancellationToken>());
            await _photoStorage.Received(1).DeleteAsync(photoKey, CancellationToken.None);
        }

        private (House House, User Reporter, Shift Shift) ArrangeShiftAssignedToAnotherMember()
        {
            var (house, reporter) = TestData.HouseWithMember("Casa");
            var assignee = TestData.NewUser("Lucia");
            house.AddMember(assignee);
            var room = new Room("Bagno");
            house.AddRoom(room);
            var assigneeTenant = house.Tenants.Single(tenant => tenant.UserId == assignee.Id);
            var shift = new Shift(house.Id, assigneeTenant, room, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);
            _houseRepository.GetByIdAsync(house.Id, Arg.Any<CancellationToken>()).Returns(house);
            _shiftRepository.GetByIdAsync(house.Id, shift.Id, Arg.Any<CancellationToken>()).Returns(shift);
            return (house, reporter, shift);
        }

        private CreateShiftReportCommandHandler CreateHandler() =>
            new(_houseRepository, _shiftRepository, _photoStorage,
                new NotificationPublisher(Substitute.For<INotificationRepository>(), TimeProvider.System, Substitute.For<IPushDispatcher>()));

        private async Task AssertNoPhotoStoredAsync() =>
            await _photoStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
    }
}
