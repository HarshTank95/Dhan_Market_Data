using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Repositories;

public interface IBacktestRunRepository
{
    Task<BacktestRun> AddAsync(BacktestRun run, CancellationToken ct = default);
    Task<BacktestRun?> GetAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<BacktestRun>> ListAsync(
        RunStatus? status = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default);
    Task UpdateAsync(BacktestRun run, CancellationToken ct = default);
    Task<int> ResetOrphanedRunsAsync(CancellationToken ct = default);
}
