using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Api.Contracts;

public sealed class StartRunRequest
{
    public int PresetId { get; init; }
    public int StockCount { get; init; } = 500;
    public int BacktestDays { get; init; } = 50;
    public string Timeframe { get; init; } = "5min";
    public string ExchangeSegment { get; init; } = "NSE_EQ";

    /// <summary>Stream a per-(stock, day) decision funnel to logs/run-{id}.jsonl. Off by default.</summary>
    public bool EnableDiagnosticLog { get; init; }
}

public class BacktestRunSummaryDto
{
    public int Id { get; init; }
    public int StrategyPresetId { get; init; }
    public string PresetName { get; init; } = "";
    public RunStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? FinishedAt { get; init; }
    public int TotalDaysProcessed { get; init; }
    public int TotalDaysPlanned { get; init; }
    public int TradeCount { get; init; }
    public decimal TotalPnL { get; init; }
    public string? ErrorMessage { get; init; }

    /// <summary>Whether the run was started with diagnostic logging requested.</summary>
    public bool DiagnosticLogEnabled { get; init; }

    /// <summary>Whether a diagnostic log file currently exists on disk for this run.</summary>
    public bool HasDiagnosticLog { get; set; }
}

public sealed class BacktestRunDetailDto : BacktestRunSummaryDto
{
    public string Timeframe { get; init; } = "";
    public string ExchangeSegment { get; init; } = "";
    public int StockCount { get; init; }
    public int BacktestDays { get; init; }
    public string ScreenerType { get; init; } = "";
    public string StrategyType { get; init; } = "";
}

public sealed class TradeRecordDto
{
    public long Id { get; init; }
    public string Symbol { get; init; } = "";
    public string SecurityId { get; init; } = "";
    public DateTime Date { get; init; }
    public DateTime EntryTime { get; init; }
    public decimal EntryPrice { get; init; }
    public int Quantity { get; init; }
    public decimal StopLoss { get; init; }
    public decimal Target { get; init; }
    public DateTime ExitTime { get; init; }
    public decimal ExitPrice { get; init; }
    public string ExitReason { get; init; } = "";
    public decimal PnL { get; init; }
    public decimal PnLPercent { get; init; }

    // Phase 9I per-trade context (nullable for legacy trades)
    public decimal? RvolAtEntry { get; init; }
    public decimal? OrWidthPct { get; init; }
    public decimal? GapPct { get; init; }
    public decimal? BreakoutCandleVolMult { get; init; }
}

public sealed class TradeListDto
{
    public IReadOnlyList<TradeRecordDto> Trades { get; init; } = Array.Empty<TradeRecordDto>();
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public sealed class StartRunResponse
{
    public int RunId { get; init; }
    public RunStatus Status { get; init; }
}

public sealed class CancelActiveResponse
{
    public int CancelledCount { get; init; }
}

public sealed class CleanupOrphansResponse
{
    public int CleanedCount { get; init; }
}

public sealed class DeleteLogsResponse
{
    public int DeletedCount { get; init; }
}
