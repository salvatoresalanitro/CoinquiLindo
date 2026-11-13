using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class RotationPlanRepository(AppDbContext dbContext) : IRotationPlanRepository
    {
        public Task<RotationPlan?> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default) =>
            dbContext.RotationPlans.FirstOrDefaultAsync(p => p.HouseId == houseId, cancellationToken);

        public Task AddAsync(RotationPlan plan, CancellationToken cancellationToken = default)
        {
            dbContext.RotationPlans.Add(plan);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        // The plan is tracked: change tracking detects the replaced lists
        public Task UpdateAsync(RotationPlan plan, CancellationToken cancellationToken = default) =>
            dbContext.SaveChangesAsync(cancellationToken);

        public Task RemoveAsync(RotationPlan plan, CancellationToken cancellationToken = default)
        {
            dbContext.RotationPlans.Remove(plan);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<RotationPlan>> GetAllAsync(CancellationToken cancellationToken = default) =>
            await dbContext.RotationPlans.AsNoTracking().ToListAsync(cancellationToken);
    }
}
