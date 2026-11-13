using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class RemoveCleaningTaskCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        public async Task Handle(RemoveCleaningTaskCommand request, CancellationToken cancellationToken)
        {
            var (house, room, author) = await CleaningTaskChanges.LoadAsync(
                houseRepository, request.HouseId, request.RoomId, request.RequesterUserId, cancellationToken);

            var task = room.RemoveCleaningTask(request.TaskId);
            await houseRepository.UpdateAsync(house, cancellationToken);

            await CleaningTaskChanges.UpdateShiftsNotStartedAsync(shiftRepository, house, room.Id, timeProvider,
                shift => shift.RemoveChecklistItem(task.Id), cancellationToken);
            await notificationPublisher.CleaningTaskRemovedAsync(house, author, room, task.Description, cancellationToken);
        }
    }
}
