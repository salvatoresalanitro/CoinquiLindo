using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class CleaningTask
    {
        public Guid Id { get; private set; }
        public string Description { get; private set; }

        public CleaningTask(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                throw new DomainValidationException("CleaningTask.DescriptionRequired", "Cleaning task description cannot be empty.");
            }

            Id = Guid.NewGuid();
            Description = description;
        }
    }
}