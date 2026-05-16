using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Volume Confluence Breakout Screener (Phase 9D-3).
///
/// 15-min opening range breakout on F&O-eligible NSE stocks, filtered by
/// cash RVOL and confirmed by F&O Open Interest direction. See
/// docs/1_strategy-rvol-orb.md for the full spec.
///
/// Long-only v1: trades only the green-OR rows from spec §5.4:
///   Long buildup    (price↑ OI↑) → confluence_w = 1.0 (full size)
///   Short covering  (price↑ OI↓) → confluence_w = 0.5 (half size)
/// Everything else (short rows, conflicts, doji OR, flat OI) → skip.
///
/// Filter chain ordered cheapest → most expensive so the typical 99%
/// of stocks that don't qualify drop out before the expensive
/// computations:
///   1.  Data sanity (intraday / daily / futures present)
///   2.  OR window built, non-doji
///   3.  Min price (≥ ₹50)
///   4.  Yesterday range % (circuit-lock proxy)
///   5.  ATR(14) ≥ MinAtrPercent of price
///   6.  Min avg daily rupee turnover (≥ ₹100 Cr)
///   7.  OR direction = green (long-only v1)
///   8.  RVOL_15min computation + MinRvol floor
///   9.  Futures OR + OI delta
///  10.  OI confluence cell mapping
///  11.  Composite score (RVOL × confluence_w) ≥ MinScoreThreshold
///
/// Signal returned:
///   Candles[0] = synthetic candle holding the cash OR bounds
///                (Open=orOpen, High=orHigh, Low=orLow, Close=orClose, Volume=orVolume)
///   SizingMultiplier = 1.0 (long buildup) or 0.5 (short covering)
///   Atr = ATR(14) value the strategy uses for stop distance
/// </summary>
public class RvolOrbScreener : IScreener
{
    private readonly RvolOrbConfig _config;

    public string Name => "RVOL + ORB + OI Confluence";
    public string Description => "F&O-eligible 15-min ORB filtered by cash RVOL and futures OI direction.";

    public int RequiredHistoricalDays => _config.MinHistoricalDays;
    public bool RequiresDailyCandles => true;
    public bool RequiresFuturesCandles => true;

    public RvolOrbScreener(RvolOrbConfig? config = null)
    {
        _config = config ?? new RvolOrbConfig();
    }

    // Legacy path: this screener needs daily + futures, so the intraday-only
    // signature returns false. The orchestrator uses MeetsSignal when the
    // flags are set.
    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        return false;
    }

    public bool MeetsSignal(ScreenerContext context, out ScreenerSignal signal)
    {
        signal = new ScreenerSignal(new List<Candle>(), 0m, 0m);

        // ── 1. Data sanity ──────────────────────────────────────────────
        var intraday = context.Intraday;
        var daily = context.Daily;
        var futures = context.Futures;

        if (intraday is null || intraday.Count == 0) return false;
        if (daily is null || daily.Count < _config.MinHistoricalDays) return false;
        if (futures is null || futures.Count == 0) return false;

        var currentDay = intraday.Last().Timestamp.Date;
        var currentIntraday = intraday.Where(c => c.Timestamp.Date == currentDay).ToList();
        if (currentIntraday.Count == 0) return false;

        var futToday = futures.Where(c => c.Timestamp.Date == currentDay).ToList();
        if (futToday.Count == 0) return false;

        // ── 2. Build cash OR window ─────────────────────────────────────
        var orWindowStart = currentIntraday.First().Timestamp;
        var orWindowEnd   = orWindowStart.AddMinutes(_config.OpeningRangeMinutes);

        var orCandles = currentIntraday.Where(c => c.Timestamp < orWindowEnd).ToList();
        if (orCandles.Count == 0) return false;

        var orOpen   = orCandles.First().Open;
        var orClose  = orCandles.Last().Close;
        var orHigh   = orCandles.Max(c => c.High);
        var orLow    = orCandles.Min(c => c.Low);
        var orVolume = orCandles.Sum(c => c.Volume);
        var orRange  = orHigh - orLow;

        // Doji rejection: |close - open| / range must exceed the threshold.
        if (orRange <= 0) return false;
        var bodyFraction = Math.Abs(orClose - orOpen) / orRange;
        if (bodyFraction < _config.DojiThreshold) return false;

        // ── 3. Min price (cheap, from OR open) ──────────────────────────
        if (orOpen < _config.MinPrice) return false;

        // ── 4. Yesterday range % (cheap, from last daily candle) ────────
        var sortedDaily = daily.OrderBy(c => c.Timestamp).ToList();
        var prevDay = sortedDaily[^1];
        if (prevDay.Close <= 0) return false;
        var prevRangePct = (prevDay.High - prevDay.Low) / prevDay.Close * 100m;
        if (prevRangePct >= _config.MaxYesterdayRangePct) return false;

        // ── 5. ATR(14) ≥ MinAtrPercent ──────────────────────────────────
        var atr = ComputeAtrWilder(sortedDaily, _config.AtrLookback);
        if (atr <= 0) return false;
        var atrPct = atr / orOpen * 100m;
        if (atrPct < _config.MinAtrPercent) return false;

        // ── 6. Min avg daily rupee turnover ─────────────────────────────
        var rupeeVolDays = Math.Min(30, sortedDaily.Count);
        var avgRupeeVol = sortedDaily
            .OrderByDescending(c => c.Timestamp)
            .Take(rupeeVolDays)
            .Average(c => (double)c.Volume * (double)c.Close);
        if (avgRupeeVol < _config.MinAvgRupeeVolume) return false;

        // ── 7. OR direction = green (long-only v1) ──────────────────────
        // Spec §5.4: skip red OR + any long-side confluence as a conflict.
        // Short-side OR (red) trades require Trade.Direction migration, deferred.
        if (orClose <= orOpen) return false;

        // ── 8. RVOL_15min: today's OR volume vs same-slot history ───────
        var sameSlotVolumes = ComputeSameSlotVolumes(
            intraday, currentDay, _config.OpeningRangeMinutes, _config.RvolLookbackDays);
        if (sameSlotVolumes.Count == 0) return false;
        var avgSlotVol = sameSlotVolumes.Average();
        if (avgSlotVol <= 0) return false;
        var rvol = (decimal)orVolume / (decimal)avgSlotVol;
        if (rvol < _config.MinRvol) return false;

        // ── 9. Futures OR + OI delta ────────────────────────────────────
        var futOrCandles = futToday.Where(c => c.Timestamp < orWindowEnd).ToList();
        if (futOrCandles.Count == 0) return false;
        var futOrOpen  = futOrCandles.First().Open;
        var futOrClose = futOrCandles.Last().Close;

        var oiStart = futOrCandles.First().OpenInterest ?? 0;
        var oiEnd   = futOrCandles.Last().OpenInterest  ?? 0;
        if (oiStart <= 0) return false;

        var oiDeltaPct = (decimal)(oiEnd - oiStart) / oiStart * 100m;
        var absOiDeltaPct = Math.Abs(oiDeltaPct);

        // ── 10. Confluence cell mapping (long-only v1) ──────────────────
        decimal confluenceW;

        if (_config.RequireOiConfluence)
        {
            // Flat OI — drop entirely per spec §5.3
            if (absOiDeltaPct < _config.MinOiDeltaPercent) return false;

            var futPriceDir = futOrClose > futOrOpen ? 1 : (futOrClose < futOrOpen ? -1 : 0);
            var oiDir       = oiDeltaPct > 0 ? 1 : -1;

            // We already filtered to green-OR above (long-only). Map only
            // the long-side cells:
            if (futPriceDir == 1 && oiDir == 1)
                confluenceW = 1.0m;          // Long buildup → full size
            else if (futPriceDir == 1 && oiDir == -1)
                confluenceW = 0.5m;          // Short covering → half size
            else
                return false;                // Conflicting or short-side cell
        }
        else
        {
            // Plain RVOL+ORB variant (toggle for §11.4 fallback) — no OI weighting.
            confluenceW = 1.0m;
        }

        // ── 11. Composite score threshold ───────────────────────────────
        var score = rvol * confluenceW;
        if (score < _config.MinScoreThreshold) return false;

        // ── All filters passed — emit signal ────────────────────────────
        // Pack OR bounds into a single synthetic candle. The strategy
        // reads .High for the stop-market arm price and .Low only for
        // diagnostics; ATR drives the stop distance.
        var orSummary = new Candle
        {
            Timestamp = orWindowStart,
            Open      = orOpen,
            High      = orHigh,
            Low       = orLow,
            Close     = orClose,
            Volume    = orVolume,
        };

        signal = new ScreenerSignal(
            Candles: new List<Candle> { orSummary },
            SizingMultiplier: confluenceW,
            Atr: atr);
        return true;
    }

    /// <summary>
    /// Wilder's ATR (RMA smoothing). Returns the most recent ATR value.
    /// Needs at least `period + 1` daily candles.
    /// </summary>
    private static decimal ComputeAtrWilder(List<Candle> sortedDaily, int period)
    {
        if (sortedDaily == null || sortedDaily.Count <= period) return 0;

        var trs = new List<decimal>(sortedDaily.Count - 1);
        for (int i = 1; i < sortedDaily.Count; i++)
        {
            var c = sortedDaily[i];
            var prevClose = sortedDaily[i - 1].Close;
            var tr = Math.Max(c.High - c.Low,
                     Math.Max(Math.Abs(c.High - prevClose), Math.Abs(c.Low - prevClose)));
            trs.Add(tr);
        }

        if (trs.Count < period) return 0;

        decimal atr = trs.Take(period).Average();
        for (int i = period; i < trs.Count; i++)
        {
            atr = ((atr * (period - 1)) + trs[i]) / period;
        }
        return atr;
    }

    /// <summary>
    /// Same-slot volume baseline for RVOL: for each prior trading day in
    /// the history, sum the volume of candles that fall in the first
    /// `orMinutes` of that day. Returns the last `days` such values.
    /// </summary>
    private static List<double> ComputeSameSlotVolumes(
        List<Candle> intraday,
        DateTime currentDay,
        int orMinutes,
        int days)
    {
        return intraday
            .Where(c => c.Timestamp.Date < currentDay)
            .GroupBy(c => c.Timestamp.Date)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.Timestamp).ToList();
                if (ordered.Count == 0) return 0.0;
                var firstTs = ordered[0].Timestamp;
                var cutoff  = firstTs.AddMinutes(orMinutes);
                return ordered.Where(c => c.Timestamp < cutoff).Sum(c => (double)c.Volume);
            })
            .Where(v => v > 0)
            .OrderByDescending(_ => 0) // preserve recency
            .Take(days)
            .ToList();
    }
}
