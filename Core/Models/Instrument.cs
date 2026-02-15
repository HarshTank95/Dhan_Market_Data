namespace DhanMarketData.Core.Models;

public class Instrument
{
    // SEM_SMST_SECURITY_ID
    public int InstrumentId { get; set; }

    // SEM_TRADING_SYMBOL
    public string TradingSymbol { get; set; } = string.Empty;

    // SEM_EXM_EXCH_ID (NSE / BSE)
    public string Exchange { get; set; } = string.Empty;

    // SEM_SEGMENT (EQ / FO / CD / COM)
    public string Segment { get; set; } = string.Empty;

    // Derived value used by Dhan APIs
    // Example: NSE + EQ => NSE_EQ
    public string ExchangeSegment =>
        Segment == "E" ? $"{Exchange}_EQ" :
        Segment == "D" ? $"{Exchange}_FNO" :
        $"{Exchange}_{Segment}";

    // SecurityId as string for API compatibility
    public string SecurityId => InstrumentId.ToString();

    // SM_SYMBOL_NAME
    public string CompanyName { get; set; } = string.Empty;
}
