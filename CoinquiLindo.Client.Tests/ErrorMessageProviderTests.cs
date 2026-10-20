using FluentAssertions;

namespace CoinquiLindo.Client.Tests
{
    public class ErrorMessageProviderTests
    {
        // Keep aligned with the errorCode list in PROJECT_BIBLE.md §6
        [Theory]
        [InlineData("House.NameRequired")]
        [InlineData("House.NotFound")]
        [InlineData("House.RoomNameAlreadyExists")]
        [InlineData("Tenant.NameRequired")]
        [InlineData("Tenant.NotFound")]
        [InlineData("Room.NameRequired")]
        [InlineData("Room.NotFound")]
        [InlineData("CleaningTask.DescriptionRequired")]
        [InlineData("Report.DescriptionRequired")]
        [InlineData("Shift.InvalidDateRange")]
        [InlineData("Shift.SelfReportNotAllowed")]
        [InlineData("ShiftChecklistItem.NotFound")]
        [InlineData("General.BadRequest")]
        [InlineData("General.Unexpected")]
        [InlineData(ApiError.NetworkUnavailableCode)]
        public void KnownCodes_Should_HaveSpecificMessage(string errorCode)
        {
            var message = ErrorMessageProvider.GetMessage(errorCode, null);

            message.Should().NotBeNullOrWhiteSpace();
            message.Should().NotBe(ErrorMessageProvider.DefaultMessage);
        }

        [Theory]
        [InlineData(400)]
        [InlineData(404)]
        [InlineData(409)]
        [InlineData(422)]
        [InlineData(500)]
        [InlineData(502)]
        [InlineData(503)]
        public void UnknownCode_Should_FallBackToStatusMessage(int status)
        {
            var message = ErrorMessageProvider.GetMessage("Unknown.Code", status);

            message.Should().NotBeNullOrWhiteSpace();
            message.Should().NotBe(ErrorMessageProvider.DefaultMessage);
        }

        [Fact]
        public void NoCodeAndUnmappedStatus_Should_ReturnDefaultMessage()
        {
            ErrorMessageProvider.GetMessage(null, 418).Should().Be(ErrorMessageProvider.DefaultMessage);
            ErrorMessageProvider.GetMessage(null, null).Should().Be(ErrorMessageProvider.DefaultMessage);
        }
    }
}
