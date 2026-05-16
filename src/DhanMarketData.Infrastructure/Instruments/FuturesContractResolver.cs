using DhanMarketData.Core.Models;

namespace DhanMarketData.Infrastructure.Data;

/// <summary>
/// Resolves the active (near-month) FUTSTK contract for an underlying
/// equity as of a given date. Required by the RVOL+ORB+OI strategy because
/// each backtest day needs to use the contract that was actually trading
/// at that point in time — not whatever's hardcoded in code or whatever's
/// "current" in instruments.csv.
///
/// Stateless wrapper around InstrumentService.GetFuturesContracts. Made
/// a separate class (not a method on InstrumentService) because Phase D
/// strategies will inject this directly without needing the full
/// instruments surface.
/// </summary>
public sealed class FuturesContractResolver
{
    private readonly InstrumentService _instruments;

    public FuturesContractResolver(InstrumentService instruments)
    {
        _instruments = instruments;
    }

    /// <summary>
    /// Returns the FUTSTK contract whose expiry is the earliest one
    /// >= <paramref name="asOfDate"/>. Returns null if no contract for
    /// this underlying covers the date.
    ///
    /// Example: ResolveNearMonth("RELIANCE", 2026-05-16) → the May 2026
    /// expiry contract because its expiry (2026-05-26) is the nearest
    /// future >= the as-of date.
    /// </summary>
    public Instrument? ResolveNearMonth(string equitySymbol, DateTime asOfDate)
    {
        if (string.IsNullOrWhiteSpace(equitySymbol)) return null;

        return _instruments.GetFuturesContracts(equitySymbol)
            .FirstOrDefault(c => c.Expiry!.Value.Date >= asOfDate.Date);
    }
}
