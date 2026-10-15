namespace CoinquiLindo.Domain.Entities
{
    public class Shift
    {
        public Guid Id { get; private set; }
        public Tenant AssignedTenant { get; private set; }
        public Room AssignedRoom { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        private readonly List<ShiftChecklistItem> _checklist = new();
        public IReadOnlyCollection<ShiftChecklistItem> Checklist => _checklist.AsReadOnly();

        public Shift(Tenant assignedTenant, Room assignedRoom, DateTime startDate, DateTime endDate)
        {
            ArgumentNullException.ThrowIfNull(assignedTenant);
            ArgumentNullException.ThrowIfNull(assignedRoom);

            if (endDate <= startDate)
            {
                throw new ArgumentException("End date must be after start date.");
            }

            Id = Guid.NewGuid();
            AssignedTenant = assignedTenant;
            AssignedRoom = assignedRoom;
            StartDate = startDate;
            EndDate = endDate;

            // Initialize the checklist with the cleaning tasks of the assigned room
            foreach (var task in assignedRoom.CleaningTasks)
            {
                _checklist.Add(new ShiftChecklistItem(task.Description));
            }
        }

        public void MarkItemAsCompleted(Guid checklistId)
        {
            var item = _checklist.FirstOrDefault(i => i.Id == checklistId);
            if (item == null)
            {
                throw new InvalidOperationException("Checklist item not found in this shift.");
            }

            item.MarkAsCompleted();
        }

        public bool IsShiftFullyCompleted => _checklist.All(c => c.IsCompleted);
    }
}
