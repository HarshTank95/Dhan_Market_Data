namespace DhanMarketData.Core.Models;

public class Instrument
{
    // SEM_SMST_SECURITY_ID
    public int InstrumentId { get; set; }

    // SEM_TRADING_SYMBOL
    public string TradingSymbol { get; set; } = string.Empty;

    // SEM_EXM_EXCH_ID (NSE / BSE)
    public string Exchange { get; set; } = string.Empty;

    // SEM_SEGMENT (E=equity, D=derivative, I=index, C=currency, M=commodity)
    public string Segment { get; set; } = string.Empty;

    // SEM_INSTRUMENT_NAME — EQUITY / FUTSTK / OPTSTK / FUTIDX / OPTIDX / INDEX / ...
    // Phase 9B addition: needed to distinguish FUTSTK contracts from OPTSTK
    // contracts within the same D-segment.
    public string InstrumentType { get; set; } = string.Empty;

    // SEM_EXPIRY_DATE — populated for derivatives, null for equity/index.
    // Phase 9B addition: needed to resolve "current near-month" FUT contract.
    public DateTime? Expiry { get; set; }

    // Derived value used by Dhan APIs.
    // E => NSE_EQ, D => NSE_FNO, I => IDX_I (per Dhan annexure, not NSE_I).
    public string ExchangeSegment =>
        Segment == "E" ? $"{Exchange}_EQ" :
        Segment == "D" ? $"{Exchange}_FNO" :
        Segment == "I" ? "IDX_I" :
        $"{Exchange}_{Segment}";

    // SecurityId as string for API compatibility
    public string SecurityId => InstrumentId.ToString();

    // SM_SYMBOL_NAME
    public string CompanyName { get; set; } = string.Empty;
}
