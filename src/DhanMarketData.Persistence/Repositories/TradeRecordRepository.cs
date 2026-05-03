using DhanMarketData.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DhanMarketData.Persistence.Repositories;

public sealed class TradeRecordRepository : ITradeRecordRepository
{
    private readonly AppDbContext _db;

    public TradeRecordRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(TradeRecord trade, CancellationToken ct = default)
    {
        _db.TradeRecords.Add(trade);
        await _db.SaveChangesAsync(ct);
    }

    public async Task AddRangeAsync(IEnumerable<TradeRecord> trades, CancellationToken ct = default)
    {
        _db.TradeRecords.AddRange(trades);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<TradeRecord> Trades, int TotalCount)> ListByRunAsync(
        int runId,
        string? exitReasonFilter = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default)
    {
        var q = _db.TradeRecords
            .AsNoTracking()
            .Where(t => t.BacktestRunId == runId);

        if (!string.IsNullOrEmpty(exitReasonFilter))
            q = q.Where(t => t.ExitReason == exitReasonFilter);

        var total = await q.CountAsync(ct);

        var trades = await q
            .OrderBy(t => t.Date).ThenBy(t => t.EntryTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (trades, total);
    }

    public IAsyncEnumerable<TradeRecord> StreamByRunAsync(int runId, CancellationToken ct = default) =>
        _db.TradeRecords
            .AsNoTracking()
            .Where(t => t.BacktestRunId == runId)
            .OrderBy(t => t.Date).ThenBy(t => t.EntryTime)
            .AsAsyncEnumerable();
}
