using DhanMarketData.Api.Contracts;
using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Api.Hubs;

public interface IBacktestHubBroadcaster
{
    Task RunStarted(int runId, int totalDaysPlanned, CancellationToken ct = default);
    Task ChunkProgress(int runId, int currentChunk, int totalChunks, int daysProcessed, CancellationToken ct = default);
    Task TradeRecorded(int runId, TradeRecordDto trade, CancellationToken ct = default);
    Task RunCompleted(int runId, RunCompletedSummary summary, CancellationToken ct = default);
    Task RunFailed(int runId, string errorMessage, CancellationToken ct = default);
    Task RunCancelled(int runId, int daysProcessed, int daysPlanned, CancellationToken ct = default);
}

public sealed class RunCompletedSummary
{
    public int TradeCount { get; init; }
    public decimal TotalPnL { get; init; }
    public double WinRate { get; init; }
    public IReadOnlyDictionary<string, int> ExitBreakdown { get; init; } =
        new Dictionary<string, int>();
}
