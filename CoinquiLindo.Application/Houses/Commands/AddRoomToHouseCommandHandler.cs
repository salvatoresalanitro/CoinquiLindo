using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Application.Houses.Commands
{
    public class AddRoomToHouseCommandHandler(IHouseRepository houseRepository)
    {
        public async Task<Guid> Handle(AddRoomToHouseCommand request, CancellationToken cancellationToken)
        {
            var house = await houseRepository.GetByIdAsync(request.HouseId, cancellationToken)
                ?? throw new NotFoundException(nameof(House), request.HouseId);

            house.EnsureIsMember(request.RequesterUserId);

            var room = new Room(request.Name?.Trim() ?? string.Empty);

            var descriptions = request.CleaningTasks
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Select(d => d.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var description in descriptions)
            {
                room.AddCleaningTask(new CleaningTask(description));
            }

            house.AddRoom(room);

            await houseRepository.UpdateAsync(house, cancellationToken);

            return room.Id;
        }
    }
}
