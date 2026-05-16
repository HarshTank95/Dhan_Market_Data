using DhanMarketData.Calendar;
using DhanMarketData.Core.Models;
using DhanMarketData.Infrastructure.Caching;
using DhanMarketData.Infrastructure.Data;

namespace DhanMarketData.Infrastructure.Quotes;

/// <summary>
/// Day-level regime breaker for the RVOL+ORB+OI strategy (§4.2 of
/// docs/1_strategy-rvol-orb.md). When VIX is too high or Nifty 50 opens
/// with too large a gap, the entire day is skipped — no per-stock
/// decisions are made.
///
/// Backtest mode: pulls Nifty 50 + India VIX intraday candles from the
/// cache for the requested day and the prior trading day. The first 15-min
/// candle of `day` gives today's open; the prior trading day's intraday
/// gives prev close.
///
/// Live mode: not implemented in Phase 9B. Live trading code can build a
/// parallel IsLiveDayTradeableAsync that calls /v2/marketfeed/ltp.
/// </summary>
public sealed class RegimeBreakerService
{
    private const string NiftyTradingSymbol = "NIFTY";
    private const string VixTradingSymbol   = "INDIA VIX";

    private readonly InstrumentService _instruments;
    private readonly HistoricalDataCache _cache;
    private readonly TradingCalendarService _calendar;

    public RegimeBreakerService(
        InstrumentService instruments,
        HistoricalDataCache cache,
        TradingCalendarService calendar)
    {
        _instruments = instruments;
        _cache = cache;
        _calendar = calendar;
    }

    /// <summary>
    /// Returns true if <paramref name="day"/> is tradeable under the
    /// regime rules. Tradeable means: India VIX (at day open) is below
    /// <paramref name="maxVix"/> AND |Nifty 50 gap%| (today open vs
    /// prev close) is within <paramref name="maxNiftyGapPercent"/>.
    ///
    /// If either index data is missing/unfetchable, defaults to
    /// <paramref name="allowOnMissingData"/> (true = trade anyway) so
    /// the regime check fails open rather than closed on data gaps.
    /// </summary>
    public async Task<bool> IsDayTradeableAsync(
        DateTime day,
        decimal maxVix = 25m,
        decimal maxNiftyGapPercent = 2m,
        bool allowOnMissingData = true,
        CancellationToken ct = default)
    {
        var nifty = _instruments.GetIndex(NiftyTradingSymbol);
        var vix   = _instruments.GetIndex(VixTradingSymbol);

        if (nifty is null || vix is null)
        {
            // Instruments file doesn't have the index rows — fail open.
            return allowOnMissingData;
        }

        var prevDay = _calendar.GetPreviousTradingDay(day);

        // Today's first candle of Nifty + VIX
        var niftyToday = await _cache.LoadOrFetchIndexAsync(
            nifty.SecurityId, day, "15min", ct: ct);
        var vixToday   = await _cache.LoadOrFetchIndexAsync(
            vix.SecurityId, day, "15min", ct: ct);

        // Previous day's last candle of Nifty (for gap%)
        var niftyPrev = await _cache.LoadOrFetchIndexAsync(
            nifty.SecurityId, prevDay, "15min", ct: ct);

        if (niftyToday.Count == 0 || vixToday.Count == 0 || niftyPrev.Count == 0)
            return allowOnMissingData;

        var todayOpen = niftyToday[0].Open;
        var prevClose = niftyPrev[^1].Close;
        if (prevClose == 0) return allowOnMissingData;

        var gapPct = Math.Abs((todayOpen - prevClose) / prevClose * 100m);
        if (gapPct > maxNiftyGapPercent) return false;

        var vixOpen = vixToday[0].Open;
        if (vixOpen >= maxVix) return false;

        return true;
    }
}
