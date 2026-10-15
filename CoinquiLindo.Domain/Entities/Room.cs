namespace CoinquiLindo.Domain.Entities
{
    public class Room
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        // add backfield to protect the cleaning tasks list from being modified directly
        private readonly List<CleaningTask> _cleaningTasks = new();
        // IReadOnlyCollection avoid the possibility of modifying the list from outside the class
        public IReadOnlyCollection<CleaningTask> CleaningTasks => _cleaningTasks.AsReadOnly();

        public Room(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Room name cannot be null or empty.", nameof(name));
            }
            Id = Guid.NewGuid();
            Name = name;
        }

        public void AddCleaningTask(CleaningTask task)
        {
            ArgumentNullException.ThrowIfNull(task, nameof(task));
            _cleaningTasks.Add(task);
        }
    }
}
