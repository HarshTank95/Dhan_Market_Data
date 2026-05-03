namespace DhanMarketData.Persistence.Entities;

public enum RunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelling,
    Cancelled
}

public class BacktestRun
{
    public int Id { get; set; }

    public int StrategyPresetId { get; set; }
    public StrategyPreset StrategyPreset { get; set; } = null!;

    // Frozen copy of preset configs at run start — historical audit trail
    public string PresetSnapshotJson { get; set; } = "";

    public int StockCount { get; set; }
    public int BacktestDays { get; set; }
    public string Timeframe { get; set; } = "";
    public string ExchangeSegment { get; set; } = "";

    public RunStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }

    public int TotalDaysProcessed { get; set; }
    public int TotalDaysPlanned { get; set; }

    // Denormalised summary so /api/runs listings render without scanning trades
    public int TradeCount { get; set; }
    public decimal TotalPnL { get; set; }

    public ICollection<TradeRecord> Trades { get; set; } = new List<TradeRecord>();
}
