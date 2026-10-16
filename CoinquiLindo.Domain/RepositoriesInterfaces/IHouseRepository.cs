using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.RepositoriesInterfaces
{
    public interface IHouseRepository
    {
        Task AddAsync(House house, CancellationToken cancellationToken = default);
        Task<House?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task UpdateAsync(House house, CancellationToken cancellationToken = default);
    }
}
