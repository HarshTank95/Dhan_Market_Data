using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Repositories;

public interface IStrategyPresetRepository
{
    Task<IReadOnlyList<StrategyPreset>> ListAsync(CancellationToken ct = default);
    Task<StrategyPreset?> GetAsync(int id, CancellationToken ct = default);
    Task<StrategyPreset?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<StrategyPreset> AddAsync(StrategyPreset preset, CancellationToken ct = default);
    Task UpdateAsync(StrategyPreset preset, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
