namespace DhanMarketData.Core.Models;

public class Trade
{
    public string Symbol { get; set; } = string.Empty;
    public string SecurityId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public int Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public string ExitReason { get; set; } = string.Empty;
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }

    // Phase 9I: per-trade context for post-hoc pattern analysis.
    // Populated by strategies that consume the rich ScreenerSignal
    // (currently ConfluenceOrbLongStrategy). Legacy strategies leave
    // these null. Cross-tab winners vs losers by these dimensions in
    // SQL after the run.
    public decimal? RvolAtEntry { get; set; }
    public decimal? OrWidthPct { get; set; }
    public decimal? GapPct { get; set; }
    public decimal? BreakoutCandleVolMult { get; set; }
}
