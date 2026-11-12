using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class EntityValidationTests
    {
        public static TheoryData<string?> BlankValues => new() { null, "", "   " };

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void House_Should_ThrowValidation_When_NameIsBlank(string? name)
        {
            var action = () => new House(name!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("House.NameRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Tenant_Should_ThrowValidation_When_NameIsBlank(string? name)
        {
            var action = () => new Tenant(name!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Tenant.NameRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Room_Should_ThrowValidation_When_NameIsBlank(string? name)
        {
            var action = () => new Room(name!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Room.NameRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void CleaningTask_Should_ThrowValidation_When_DescriptionIsBlank(string? description)
        {
            var action = () => new CleaningTask(description!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("CleaningTask.DescriptionRequired");
        }

        [Theory]
        [MemberData(nameof(BlankValues))]
        public void Report_Should_ThrowValidation_When_DescriptionIsBlank(string? description)
        {
            var action = () => new Report(new Tenant("Salvo"), description!, "reports/photo.jpg");

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Report.DescriptionRequired");
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-30)]
        public void Shift_Should_ThrowValidation_When_EndDateIsBeforeStartDate(int daysOffset)
        {
            var start = new DateOnly(2026, 11, 2);

            var action = () => new Shift(Guid.NewGuid(), new Tenant("Salvo"), new Room("Bagno"),
                start, start.AddDays(daysOffset), TimeZoneInfo.Utc);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Shift.InvalidDateRange");
        }

        [Theory]
        [InlineData(null, "Europe/Rome")]
        [InlineData("  ", "Europe/Rome")]
        [InlineData("America/New_York", "America/New_York")]
        [InlineData(" Europe/Lisbon ", "Europe/Lisbon")]
        // Windows ids (sent by desktop clients) are stored as IANA so every server OS resolves them
        [InlineData("W. Europe Standard Time", "Europe/Berlin")]
        public void House_Should_StoreIanaTimeZone(string? timeZoneId, string expected)
        {
            var house = new House("Casa", timeZoneId);

            house.TimeZoneId.Should().Be(expected);
            house.GetTimeZone().BaseUtcOffset.Should().Be(TimeZoneInfo.FindSystemTimeZoneById(expected).BaseUtcOffset);
        }

        [Theory]
        [InlineData("Mars/Olympus_Mons")]
        [InlineData("not a time zone")]
        public void House_Should_ThrowValidation_When_TimeZoneIsUnknown(string timeZoneId)
        {
            var action = () => new House("Casa", timeZoneId);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("House.TimeZoneInvalid");
        }

        [Fact]
        public void Shift_Should_ThrowValidation_When_HouseIdIsEmpty()
        {
            var action = () => new Shift(Guid.Empty, new Tenant("Salvo"), new Room("Bagno"),
                new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 8), TimeZoneInfo.Utc);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Shift.HouseRequired");
        }

        [Fact]
        public void NullArguments_Should_RemainProgrammingErrors()
        {
            var action = () => new House("Casa").AddRoom(null!);

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
