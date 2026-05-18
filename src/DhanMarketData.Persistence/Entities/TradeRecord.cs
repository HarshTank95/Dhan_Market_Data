namespace DhanMarketData.Persistence.Entities;

public class TradeRecord
{
    public long Id { get; set; }

    public int BacktestRunId { get; set; }
    public BacktestRun BacktestRun { get; set; } = null!;

    public string Symbol { get; set; } = "";
    public string SecurityId { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public int Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public string ExitReason { get; set; } = "";
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }

    // Phase 9I: per-trade context (nullable so existing rows stay valid).
    public decimal? RvolAtEntry { get; set; }
    public decimal? OrWidthPct { get; set; }
    public decimal? GapPct { get; set; }
    public decimal? BreakoutCandleVolMult { get; set; }
}
