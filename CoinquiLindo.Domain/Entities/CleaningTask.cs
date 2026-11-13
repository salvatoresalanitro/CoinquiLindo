using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class CleaningTask
    {
        public Guid Id { get; private set; }
        public string Description { get; private set; }

        public CleaningTask(string description)
        {
            Id = Guid.NewGuid();
            Description = Normalize(description);
        }

        // Renamed through the room, which checks duplicates
        internal void Rename(string description) => Description = Normalize(description);

        internal static string Normalize(string? description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new DomainValidationException("CleaningTask.DescriptionRequired", "Cleaning task description cannot be empty.");
            }

            return description.Trim();
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private CleaningTask() { }
#pragma warning restore CS8618
    }
}
