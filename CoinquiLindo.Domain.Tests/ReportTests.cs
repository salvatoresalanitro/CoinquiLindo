using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using FluentAssertions;

namespace CoinquiLindo.Domain.Tests
{
    public class ReportTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void CreateReport_Should_RejectMissingPhoto(string? photoKey)
        {
            var action = () => new Report(new Tenant("Salvo"), "Pavimento ancora sporco", photoKey!);

            action.Should().Throw<DomainValidationException>()
                .Which.Code.Should().Be("Report.PhotoRequired");
        }

        [Fact]
        public void CreateReport_Should_InitializeCorrectly_WithPhotoKey()
        {
            //Arrange
            var reporter = new Tenant("Salvo");
            var description = "Non hai pulito la polvere sopra il frigo";
            var photoKey = "0f8fad5bd9cb469fa165708167e1f3e1.jpg";

            //Act
            var report = new Report(reporter, description, photoKey);

            //Assert
            report.Reporter.Name.Should().Be("Salvo");
            report.Description.Should().Be(description);
            report.PhotoKey.Should().Be(photoKey);
            report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }
}
