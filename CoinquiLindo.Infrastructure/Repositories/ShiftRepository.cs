using CoinquiLindo.Domain.Entities;
using CoinquiLindo.Domain.Exceptions;
using CoinquiLindo.Domain.RepositoriesInterfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CoinquiLindo.Infrastructure.Repositories
{
    public class ShiftRepository(AppDbContext dbContext) : IShiftRepository
    {
        private const int SqliteUniqueConstraintError = 2067;

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
                .Include(shift => shift.Reports).ThenInclude(report => report.Reporter)
                .Include(shift => shift.Reports).ThenInclude(report => report.Votes)
                .AsSplitQuery()
                .OrderByDescending(shift => shift.StartDate)
                .ThenBy(shift => shift.Id)
                .ToListAsync(cancellationToken);

        public Task<Shift?> GetByIdAsync(Guid houseId, Guid shiftId, CancellationToken cancellationToken = default) =>
            dbContext.Shifts
                .Include(shift => shift.AssignedTenant)
                .Include(shift => shift.AssignedRoom)
                .Include(shift => shift.Checklist)
                .Include(shift => shift.Reports).ThenInclude(report => report.Reporter)
                .Include(shift => shift.Reports).ThenInclude(report => report.Votes)
                .AsSplitQuery()
                .FirstOrDefaultAsync(shift => shift.HouseId == houseId && shift.Id == shiftId, cancellationToken);

        public async Task<IReadOnlyList<Shift>> GetDueBetweenAsync(DateTime fromExclusive, DateTime toInclusive, CancellationToken cancellationToken = default) =>
            await dbContext.Shifts
                .AsNoTracking()
                .Where(shift => shift.DueDate > fromExclusive && shift.DueDate <= toInclusive)
                .Include(shift => shift.AssignedTenant)
                .Include(shift => shift.AssignedRoom)
                .Include(shift => shift.Checklist)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);

        public async Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            // Same vote inserted twice, or changed/removed after another request already removed it
            catch (DbUpdateConcurrencyException ex) when (ex.Entries.Count > 0 && ex.Entries.All(entry => entry.Entity is ReportVote))
            {
                throw new ReportVoteConflictException();
            }
            catch (DbUpdateException ex) when (
                ex.InnerException is SqliteException { SqliteExtendedErrorCode: SqliteUniqueConstraintError } sqlite
                && sqlite.Message.Contains("ReportVotes.", StringComparison.Ordinal))
            {
                throw new ReportVoteConflictException();
            }
        }
    }
}
