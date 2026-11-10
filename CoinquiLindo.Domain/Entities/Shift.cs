using CoinquiLindo.Domain.Enums;
using CoinquiLindo.Domain.Exceptions;

namespace CoinquiLindo.Domain.Entities
{
    public class Shift
    {
        public Guid Id { get; private set; }
        public Guid HouseId { get; private set; }
        public Tenant AssignedTenant { get; private set; }
        public Room AssignedRoom { get; private set; }
        public DateTime StartDate { get; private set; }
        public DateTime EndDate { get; private set; }

        private readonly List<ShiftChecklistItem> _checklist = new();
        private readonly List<Report> _reports = new();

        public IReadOnlyCollection<Report> Reports => _reports.AsReadOnly();
        public IReadOnlyCollection<ShiftChecklistItem> Checklist => _checklist.AsReadOnly();

        public Shift(Guid houseId, Tenant assignedTenant, Room assignedRoom, DateTime startDate, DateTime endDate)
        {
            if (houseId == Guid.Empty)
            {
                throw new DomainValidationException("Shift.HouseRequired", "A shift must belong to a house.");
            }

            ArgumentNullException.ThrowIfNull(assignedTenant, nameof(assignedTenant));
            ArgumentNullException.ThrowIfNull(assignedRoom, nameof(assignedRoom));

            if (endDate <= startDate)
            {
                throw new DomainValidationException("Shift.InvalidDateRange", "End date must be after start date.");
            }

            Id = Guid.NewGuid();
            HouseId = houseId;
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

        public bool CanReport(Guid tenantId) => tenantId != AssignedTenant.Id;

        // The reporter already backs the report and the assignee has a conflict of interest
        public bool CanVoteOnReport(Report report, Guid tenantId) =>
            tenantId != report.Reporter.Id && tenantId != AssignedTenant.Id;

        public void VoteReport(Guid reportId, Tenant voter, VoteType type)
        {
            ArgumentNullException.ThrowIfNull(voter, nameof(voter));

            if (!Enum.IsDefined(type))
            {
                throw new DomainValidationException("ReportVote.InvalidType", "A vote must be Up or Down.");
            }

            var report = GetReport(reportId);
            if (!CanVoteOnReport(report, voter.Id))
            {
                throw new ReportVoteNotAllowedException();
            }

            report.SetVote(voter.Id, type);
        }

        public void RemoveReportVote(Guid reportId, Guid voterTenantId) =>
            GetReport(reportId).RemoveVote(voterTenantId);

        public Report GetReport(Guid reportId) =>
            _reports.FirstOrDefault(report => report.Id == reportId)
                ?? throw new NotFoundException(nameof(Report), reportId);

        public bool IsShiftFullyCompleted => _checklist.All(c => c.IsCompleted);

#pragma warning disable CS8618 // It's needed for EF Core to work properly, but we don't want to allow it to be used outside of EF Core.
        private Shift() { }
#pragma warning restore CS8618
    }
}
