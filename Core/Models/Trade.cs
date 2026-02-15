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
}
