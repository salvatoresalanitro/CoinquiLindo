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
            var action = () => new Report(new Tenant("Salvo"), description!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Report.DescriptionRequired");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Shift_Should_ThrowValidation_When_EndDateIsNotAfterStartDate(int daysOffset)
        {
            var start = DateTime.UtcNow;

            var action = () => new Shift(new Tenant("Salvo"), new Room("Bagno"), start, start.AddDays(daysOffset));

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Shift.InvalidDateRange");
        }

        [Fact]
        public void NullArguments_Should_RemainProgrammingErrors()
        {
            var action = () => new House("Casa").AddRoom(null!);

            action.Should().Throw<ArgumentNullException>();
        }
    }
}
