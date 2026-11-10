using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class Report
    {
        private readonly List<ReportVote> _votes = new();

        public Guid Id { get; private set; }
        public Tenant Reporter { get; private set; }
        public string Description { get; private set; }
        // Opaque storage key of the uploaded photo (never a client-supplied URL)
        public string PhotoKey { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public IReadOnlyCollection<ReportVote> Votes => _votes.AsReadOnly();
        public int UpvoteCount => _votes.Count(vote => vote.Type == VoteType.Up);
        public int DownvoteCount => _votes.Count(vote => vote.Type == VoteType.Down);
        public int Score => _votes.Sum(vote => (int)vote.Type);

        public VoteType? VoteOf(Guid tenantId) =>
            _votes.FirstOrDefault(vote => vote.VoterTenantId == tenantId)?.Type;

        public Report(Tenant reporter, string description, string photoKey)
        {
            ArgumentNullException.ThrowIfNull(reporter, nameof(reporter));

            if(string.IsNullOrWhiteSpace(description))
            {
                throw new DomainValidationException("Report.DescriptionRequired", "Report description cannot be empty.");
            }

            if (string.IsNullOrWhiteSpace(photoKey))
            {
                throw new DomainValidationException("Report.PhotoRequired", "A report requires a photo.");
            }

            Id = Guid.NewGuid();
            Reporter = reporter;
            Description = description;
            PhotoKey = photoKey;
            CreatedAt = DateTime.UtcNow;
        }

        // Voting rules depend on the shift assignee: only the Shift aggregate calls these
        internal void SetVote(Guid voterTenantId, VoteType type)
        {
            var existing = _votes.FirstOrDefault(vote => vote.VoterTenantId == voterTenantId);
            if (existing is null)
            {
                _votes.Add(new ReportVote(voterTenantId, type));
            }
            else
            {
                existing.ChangeTo(type);
            }
        }

        internal void RemoveVote(Guid voterTenantId) =>
            _votes.RemoveAll(vote => vote.VoterTenantId == voterTenantId);

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Report() { }
#pragma warning restore CS8618
    }
}
