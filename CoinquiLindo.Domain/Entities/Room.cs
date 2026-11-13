using CoinquiLindo.Domain.Exceptions;

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
                throw new DomainValidationException("Room.NameRequired", "Room name cannot be empty.");
            }
            Id = Guid.NewGuid();
            Name = name;
        }

        public void AddCleaningTask(CleaningTask task)
        {
            ArgumentNullException.ThrowIfNull(task, nameof(task));
            EnsureIsUnique(task.Description, exceptTaskId: null);
            _cleaningTasks.Add(task);
        }

        // Returns the previous description (used by notifications)
        public string RenameCleaningTask(Guid taskId, string description)
        {
            var task = GetCleaningTask(taskId);
            var normalized = CleaningTask.Normalize(description);
            EnsureIsUnique(normalized, exceptTaskId: taskId);

            var previous = task.Description;
            task.Rename(normalized);
            return previous;
        }

        // Returns the removed task (used by notifications and to update the shifts)
        public CleaningTask RemoveCleaningTask(Guid taskId)
        {
            var task = GetCleaningTask(taskId);
            _cleaningTasks.Remove(task);
            return task;
        }

        private CleaningTask GetCleaningTask(Guid taskId) =>
            _cleaningTasks.FirstOrDefault(t => t.Id == taskId) ?? throw new NotFoundException(nameof(CleaningTask), taskId);

        // Same rule as room names: trimmed, case-insensitive
        private void EnsureIsUnique(string description, Guid? exceptTaskId)
        {
            if (_cleaningTasks.Any(t => t.Id != exceptTaskId
                && string.Equals(t.Description.Trim(), description.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                throw new CleaningTaskAlreadyExistsException();
            }
        }

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Room() { }
#pragma warning restore CS8618
    }
}
