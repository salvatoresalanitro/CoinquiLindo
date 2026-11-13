using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class UpdateCleaningTaskCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        public async Task Handle(UpdateCleaningTaskCommand request, CancellationToken cancellationToken)
        {
            var (house, room, author) = await CleaningTaskChanges.LoadAsync(
                houseRepository, request.HouseId, request.RoomId, request.RequesterUserId, cancellationToken);

            var previous = room.RenameCleaningTask(request.TaskId, request.Description);
            var description = room.CleaningTasks.Single(t => t.Id == request.TaskId).Description;
            await houseRepository.UpdateAsync(house, cancellationToken);

            await CleaningTaskChanges.UpdateShiftsNotStartedAsync(shiftRepository, house, room.Id, timeProvider,
                shift => shift.RenameChecklistItem(request.TaskId, description), cancellationToken);

            // Nothing to tell when only spaces changed
            if (previous != description)
            {
                await notificationPublisher.CleaningTaskRenamedAsync(house, author, room, previous, description, cancellationToken);
            }
        }
    }
}
