using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.Repositories
{
    public interface IShiftRepository
    {
        Task AddAsync(Shift shift, CancellationToken cancellationToken = default);
    }
}
