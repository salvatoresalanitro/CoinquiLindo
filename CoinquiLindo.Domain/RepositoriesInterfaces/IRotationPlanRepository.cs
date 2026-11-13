using CoinquiLindo.Domain.Entities;

namespace CoinquiLindo.Domain.RepositoriesInterfaces
{
    public interface IRotationPlanRepository
    {
        Task<RotationPlan?> GetByHouseIdAsync(Guid houseId, CancellationToken cancellationToken = default);
        Task AddAsync(RotationPlan plan, CancellationToken cancellationToken = default);
        Task UpdateAsync(RotationPlan plan, CancellationToken cancellationToken = default);
        Task RemoveAsync(RotationPlan plan, CancellationToken cancellationToken = default);
        // Cross-house read used by the rotation job
        Task<IReadOnlyList<RotationPlan>> GetAllAsync(CancellationToken cancellationToken = default);
    }
}
