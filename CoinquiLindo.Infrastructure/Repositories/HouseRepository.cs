using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class HouseRepository(AppDbContext dbContext) : IHouseRepository
    {
        public Task AddAsync(House house, CancellationToken cancellationToken = default)
        {
            dbContext.Houses.Add(house);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<House?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await HousesWithChildren()
                .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyList<House>> GetByMemberAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return await HousesWithChildren()
                .Where(h => h.Tenants.Any(t => t.UserId == userId))
                .ToListAsync(cancellationToken);
        }

        public Task UpdateAsync(House house, CancellationToken cancellationToken = default)
        {
            // House is already tracked: Update() would mark new children as Modified
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        // Always load the full aggregate so domain invariants (e.g. membership) work on returned houses
        private IQueryable<House> HousesWithChildren() => dbContext.Houses
            .Include(h => h.Tenants)
            .Include(h => h.Rooms)
                .ThenInclude(r => r.CleaningTasks);
    }
}
