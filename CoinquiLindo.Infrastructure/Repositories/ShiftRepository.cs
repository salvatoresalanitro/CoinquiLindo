using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class ShiftRepository(AppDbContext dbContext) : IShiftRepository
    {
        public Task AddAsync(Shift shift, CancellationToken cancellationToken = default)
        {
            dbContext.Shifts.Add(shift);
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<Shift>> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default) =>
            await dbContext.Shifts
                .AsNoTracking()
                .Where(shift => shift.HouseId == houseId)
                .Include(shift => shift.AssignedTenant)
                .Include(shift => shift.AssignedRoom)
                .Include(shift => shift.Checklist)
                .OrderByDescending(shift => shift.StartDate)
                .ThenBy(shift => shift.Id)
                .ToListAsync(cancellationToken);

        public Task<Shift?> GetByIdAsync(Guid houseId, Guid shiftId, CancellationToken cancellationToken = default) =>
            dbContext.Shifts
                .Include(shift => shift.Checklist)
                .FirstOrDefaultAsync(shift => shift.HouseId == houseId && shift.Id == shiftId, cancellationToken);

        public Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default) =>
            dbContext.SaveChangesAsync(cancellationToken);
    }
}
