using CoinquiLindo.Domain.Enums;

namespace CoinquiLindo.Domain.Entities
{
    public class ReportVote
    {
        public Guid Id { get; private set; }
        public Guid VoterTenantId { get; private set; }
        public VoteType Type { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        internal ReportVote(Guid voterTenantId, VoteType type)
        {
            Id = Guid.NewGuid();
            VoterTenantId = voterTenantId;
            Type = type;
            CreatedAt = DateTime.UtcNow;
        }

        internal void ChangeTo(VoteType type)
        {
            if (Type == type)
            {
                return;
            }

            Type = type;
            UpdatedAt = DateTime.UtcNow;
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private ReportVote() { }
#pragma warning restore CS8618
    }
}
