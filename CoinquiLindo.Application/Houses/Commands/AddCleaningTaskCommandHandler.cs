using CoinquiLindo.Application.Notifications;
using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class AddCleaningTaskCommandHandler(
        IHouseRepository houseRepository,
        IShiftRepository shiftRepository,
        NotificationPublisher notificationPublisher,
        TimeProvider timeProvider)
    {
        public async Task<Guid> Handle(AddCleaningTaskCommand request, CancellationToken cancellationToken)
        {
            var (house, room, author) = await CleaningTaskChanges.LoadAsync(
                houseRepository, request.HouseId, request.RoomId, request.RequesterUserId, cancellationToken);

            var task = new CleaningTask(request.Description);
            room.AddCleaningTask(task);
            await houseRepository.UpdateAsync(house, cancellationToken);

            await CleaningTaskChanges.UpdateShiftsNotStartedAsync(shiftRepository, house, room.Id, timeProvider,
                shift => shift.AddChecklistItem(task), cancellationToken);
            await notificationPublisher.CleaningTaskAddedAsync(house, author, room, task.Description, cancellationToken);

            return task.Id;
        }
    }
}
