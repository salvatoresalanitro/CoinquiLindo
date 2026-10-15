using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.Tests
{
    public class ReportTests
    {
        [Fact]
        public void CreateReport_Should_InitializeCorrectly_WithPhotoUrl()
        {
            //Arrange
            var reporter = new Tenant("Salvo");
            var description = "Non hai pulito la polvere sopra il frigo";
            var photoUrl = "./images/reports/cucina_sporca.jpg";

            //Act
            var report = new Report(reporter, description, photoUrl);

            //Assert
            report.Reporter.Name.Should().Be("Salvo");
            report.Description.Should().Be(description);
            report.PhotoUrl.Should().Be(photoUrl);
            report.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }
    }
}
