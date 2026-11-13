namespace CoinquiLindo.Domain.Entities
{
    public class ShiftChecklistItem
    {
        public Guid Id { get; private set; }
        public string Description { get; private set; }
        public bool IsCompleted { get; private set; }
        public DateTime? CompletedAt { get; private set; }
        // Cleaning task of the room this item was copied from (plain reference): lets task edits reach the shifts not started yet.
        // Null for items whose task could not be matched when the column was introduced
        public Guid? CleaningTaskId { get; private set; }

        internal ShiftChecklistItem(string description, Guid? cleaningTaskId = null)
        {
            Id = Guid.NewGuid();
            Description = description;
            CleaningTaskId = cleaningTaskId;
            IsCompleted = false;
        }

        internal void Rename(string description) => Description = description;

        internal void MarkAsCompleted()
        {
            IsCompleted = true;
            CompletedAt = DateTime.UtcNow;
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private ShiftChecklistItem() { }
#pragma warning restore CS8618
    }
}
