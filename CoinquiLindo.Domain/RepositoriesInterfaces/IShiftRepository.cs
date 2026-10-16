using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.RepositoriesInterfaces
{
    public interface IShiftRepository
    {
        Task AddAsync(Shift shift, CancellationToken cancellationToken = default);
    }
}
