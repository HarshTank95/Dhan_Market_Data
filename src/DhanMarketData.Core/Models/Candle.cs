namespace DhanMarketData.Core.Models;

public class Candle
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    // Open Interest — populated only for F&O (FUTSTK/FUTIDX/OPTSTK/OPTIDX)
    // when the fetch path requests it (oi=true). Null for cash/index candles
    // and for older cached files written before OI was tracked.
    public long? OpenInterest { get; set; }
}
