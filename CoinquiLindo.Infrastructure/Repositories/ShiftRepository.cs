using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class ShiftRepository(AppDbContext dbContext) : IShiftRepository
    {
        public Task AddAsync(Shift shift, CancellationToken cancellationToken = default)
        {
            dbContext.Shifts.Add(shift);
            return dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
