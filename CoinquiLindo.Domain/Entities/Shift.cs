using CoinquiLindo.Domain.Exceptions;

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
        private readonly List<Report> _reports = new();

        public IReadOnlyCollection<Report> Reports => _reports.AsReadOnly();
        public IReadOnlyCollection<ShiftChecklistItem> Checklist => _checklist.AsReadOnly();

        public Shift(Tenant assignedTenant, Room assignedRoom, DateTime startDate, DateTime endDate)
        {
            ArgumentNullException.ThrowIfNull(assignedTenant, nameof(assignedTenant));
            ArgumentNullException.ThrowIfNull(assignedRoom, nameof(assignedRoom));

            if (endDate <= startDate)
            {
                throw new DomainValidationException("Shift.InvalidDateRange", "End date must be after start date.");
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
                throw new NotFoundException(nameof(ShiftChecklistItem), checklistId);
            }

            item.MarkAsCompleted();
        }

        public void AddReport(Report report)
        {
            ArgumentNullException.ThrowIfNull(report, nameof(report));

            if(report.Reporter.Id == AssignedTenant.Id)
            {
                throw new SelfReportNotAllowedException();
            }

            _reports.Add(report);
        }

        public bool IsShiftFullyCompleted => _checklist.All(c => c.IsCompleted);

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Shift() { }
#pragma warning restore CS8618
    }
}
