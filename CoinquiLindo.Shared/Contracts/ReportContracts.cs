using System.Text.Json.Serialization;

namespace CoinquiLindo.Shared.Contracts
{
    // Serialized as "Up"/"Down"; numeric values mirror the domain VoteType
    [JsonConverter(typeof(JsonStringEnumConverter<ReportVoteValue>))]
    public enum ReportVoteValue
    {
        Down = -1,
        Up = 1
    }

    public record CastReportVoteRequest(ReportVoteValue Vote);

    // MyVote/CanVote are computed for the authenticated requester
    public record ReportResponse(
        Guid Id,
        Guid ShiftId,
        Guid ReporterTenantId,
        string ReporterName,
        string Description,
        string PhotoUrl,
        DateTime CreatedAt,
        int UpvoteCount,
        int DownvoteCount,
        int Score,
        ReportVoteValue? MyVote,
        bool CanVote);

    // multipart/form-data contract of POST /api/houses/{houseId}/shifts/{shiftId}/reports
    public static class ReportForm
    {
        public const string DescriptionField = "description";
        public const string PhotoField = "photo";
        public const int MaxPhotoBytes = 5 * 1024 * 1024;
    }
}
