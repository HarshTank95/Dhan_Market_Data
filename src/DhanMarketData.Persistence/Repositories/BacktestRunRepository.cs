using DhanMarketData.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Persistence.Repositories;

public sealed class BacktestRunRepository : IBacktestRunRepository
{
    private readonly AppDbContext _db;

    public BacktestRunRepository(AppDbContext db) => _db = db;

    public async Task<BacktestRun> AddAsync(BacktestRun run, CancellationToken ct = default)
    {
        _db.BacktestRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public Task<BacktestRun?> GetAsync(int id, CancellationToken ct = default) =>
        _db.BacktestRuns
            .Include(r => r.StrategyPreset)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<BacktestRun>> ListAsync(
        RunStatus? status = null,
        int limit = 50,
        int offset = 0,
        CancellationToken ct = default)
    {
        var q = _db.BacktestRuns
            .AsNoTracking()
            .Include(r => r.StrategyPreset)
            .OrderByDescending(r => r.CreatedAt)
            .AsQueryable();

        if (status is not null)
            q = q.Where(r => r.Status == status);

        return await q.Skip(offset).Take(limit).ToListAsync(ct);
    }

    public Task UpdateAsync(BacktestRun run, CancellationToken ct = default)
    {
        _db.BacktestRuns.Update(run);
        return _db.SaveChangesAsync(ct);
    }

    // Called on API startup — runs left in Running/Cancelling state from a previous
    // process crash get marked Failed so they don't appear active forever.
    public async Task<int> ResetOrphanedRunsAsync(CancellationToken ct = default)
    {
        var orphans = await _db.BacktestRuns
            .Where(r => r.Status == RunStatus.Running || r.Status == RunStatus.Cancelling)
            .ToListAsync(ct);

        foreach (var run in orphans)
        {
            run.Status = RunStatus.Failed;
            run.ErrorMessage = "Server restarted during run.";
            run.FinishedAt = DateTime.UtcNow;
        }

        if (orphans.Count > 0)
            await _db.SaveChangesAsync(ct);

        return orphans.Count;
    }
}
