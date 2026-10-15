namespace CoinquiLindo.Domain.Entities
{
    public class ShiftChecklistItem
    {
        public Guid Id { get; private set; }
        public string Description { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime? CompletedAt { get; private set; }

        internal ShiftChecklistItem(string description)
        {
            Id = Guid.NewGuid();
            Description = description;
            IsCompleted = false;
        }

        internal void MarkAsCompleted()
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
        }
    }
}
