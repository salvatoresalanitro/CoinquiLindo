namespace CoinquiLindo.Domain.Entities
{
    public class Report
    {
        public Guid Id { get; private set; }
        public Tenant Reporter { get; private set; }
        public string Description { get; private set; }
        public string? PhotoUrl { get; private set; }
        public DateTime CreatedAt { get; private set; }

        public Report(Tenant reporter, string description, string? photoUrl = null)
        {
            ArgumentNullException.ThrowIfNull(reporter, nameof(reporter));

            if(string.IsNullOrWhiteSpace(description))
            {
                throw new ArgumentException("Description cannot be empty.", nameof(description));
            }

            Id = Guid.NewGuid();
            Reporter = reporter;
            Description = description;
            PhotoUrl = photoUrl;
            CreatedAt = DateTime.UtcNow;
        }

    }
}
