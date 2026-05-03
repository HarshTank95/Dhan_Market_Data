using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Repositories;

public interface ITradeRecordRepository
{
    Task AddAsync(TradeRecord trade, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<TradeRecord> trades, CancellationToken ct = default);
    Task<(IReadOnlyList<TradeRecord> Trades, int TotalCount)> ListByRunAsync(
        int runId,
        string? exitReasonFilter = null,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default);
    IAsyncEnumerable<TradeRecord> StreamByRunAsync(int runId, CancellationToken ct = default);
}
