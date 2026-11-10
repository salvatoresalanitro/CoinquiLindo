using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class Report
    {
        public Guid Id { get; private set; }
        public Tenant Reporter { get; private set; }
        public string Description { get; private set; }
        // Opaque storage key of the uploaded photo (never a client-supplied URL)
        public string PhotoKey { get; private set; }
        public DateTime CreatedAt { get; private set; }

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

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Report() { }
#pragma warning restore CS8618
    }
}
