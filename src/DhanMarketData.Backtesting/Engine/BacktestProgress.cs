using DhanMarketData.Core.Models;

namespace DhanMarketData.Backtest;

// Event payload emitted by BacktestOrchestrator through IProgress<BacktestProgress>.
// Translated by the API's BacktestRunner into:
//   - SignalR events to the UI (RunStarted, ChunkProgress, TradeRecorded, RunCompleted)
//   - TradeRecord rows in SQLite (on TradeRecorded events)
public enum BacktestEventKind
{
    Started,
    ChunkProgress,
    TradeRecorded,
    Finished,
}

public sealed record BacktestProgress(
    BacktestEventKind Kind,
    int TotalDaysPlanned,
    int DaysProcessed,
    int CurrentChunk,
    int TotalChunks,
    Trade? Trade,
    DateTime? Day);
