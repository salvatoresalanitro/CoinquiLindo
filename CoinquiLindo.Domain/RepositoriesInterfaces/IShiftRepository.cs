using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.RepositoriesInterfaces
{
    public interface IShiftRepository
    {
        Task AddAsync(Shift shift, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Shift>> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);
        Task<Shift?> GetByIdAsync(Guid houseId, Guid shiftId, CancellationToken cancellationToken = default);
        Task UpdateAsync(Shift shift, CancellationToken cancellationToken = default);
        // Cross-house read used by the due-soon reminder job: EndDate in (fromExclusive, toInclusive]
        Task<IReadOnlyList<Shift>> GetEndingBetweenAsync(DateTime fromExclusive, DateTime toInclusive, CancellationToken cancellationToken = default);
    }
}
