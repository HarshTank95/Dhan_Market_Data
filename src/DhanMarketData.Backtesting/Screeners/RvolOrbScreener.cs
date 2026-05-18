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
    // Futures data is needed ONLY when the OI confluence overlay is on.
    // When `RequireOiConfluence = false`, the screener degrades to plain
    // RVOL+ORB and futures fetches are wasted bandwidth — skip them.
    public bool RequiresFuturesCandles => _config.RequireOiConfluence;
    public bool RequiresRegimeBreaker => true;
    public decimal MaxVixThreshold => _config.SkipDayIfIndiaVixAbove;
    public decimal MaxNiftyGapPctThreshold => _config.SkipDayIfNiftyGapPct;

    public RvolOrbScreener(RvolOrbConfig? config = null)
    {
        _config = config ?? new RvolOrbConfig();
    }

    // ── Filter-funnel instrumentation ───────────────────────────────────
    // Counters accumulate across every MeetsSignal call within a chunk.
    // The orchestrator calls LogDiagnostics at end-of-chunk; we print +
    // reset there. Names ordered to match the 11-step filter chain in
    // MeetsSignal — keep them in sync if a step is added/reordered.
    private const int F_NoIntraday    = 0;
    private const int F_NoDaily       = 1;
    private const int F_NoFutures     = 2;
    private const int F_OrDoji        = 3;   // OR window unbuildable or doji-shaped
    private const int F_MinPrice      = 4;
    private const int F_YdayRange     = 5;   // circuit-lock proxy
    private const int F_AtrPct        = 6;
    private const int F_RupeeVol      = 7;
    private const int F_OrRed         = 8;   // long-only v1: red OR is dropped
    private const int F_RvolFloor     = 9;
    private const int F_OiUnavailable = 10;  // no futures OR bar or OI_start == 0
    private const int F_OiDeltaFlat   = 11;  // |ΔOI| below MinOiDeltaPercent
    private const int F_OiConflict    = 12;  // confluence cell rejected (conflict / short-side)
    private const int F_ScoreLow      = 13;  // score below MinScoreThreshold
    private const int F_Passed        = 14;  // emitted a signal
    private const int F_SkippedDay    = 15;  // day-of-week filter (e.g. SkipTuesday)
    private const int F_GapNotDown    = 16;  // gap doesn't satisfy MinGapPct (e.g. flat or gap-up)

    private static readonly string[] FunnelLabels = new[]
    {
        "no intraday data",
        "no daily data",
        "no futures data",
        "OR doji / zero range",
        $"min price",
        "yesterday range (circuit-lock proxy)",
        "ATR % below floor",
        "avg ₹ volume below floor",
        "OR direction red (long-only)",
        "RVOL below MinRvol",
        "futures OR / OI unavailable",
        "OI delta below MinOiDeltaPercent",
        "OI confluence rejected",
        "score below MinScoreThreshold",
        "PASSED → emitted signal",
        "day-of-week skipped (e.g. Tuesday)",
        "gap not down enough (MinGapPct)",
    };

    private readonly long[] _funnel = new long[FunnelLabels.Length];

    private bool Drop(int step, out ScreenerSignal signal)
    {
        _funnel[step]++;
        signal = new ScreenerSignal(new List<Candle>(), 0m, 0m);
        return false;
    }

    public void LogDiagnostics(string context)
    {
        var total = 0L;
        for (int i = 0; i < _funnel.Length; i++) total += _funnel[i];
        if (total == 0) return;  // chunk never invoked the screener — nothing to say

        Console.WriteLine();
        Console.WriteLine($"=== RvolOrbScreener funnel — {context} ===");
        Console.WriteLine($"  Total evaluations           : {total,8:N0}");
        for (int i = 0; i < _funnel.Length; i++)
        {
            if (_funnel[i] == 0 && i != F_Passed) continue;  // hide zero rows except PASSED
            var pct = total > 0 ? (double)_funnel[i] / total * 100.0 : 0.0;
            var prefix = i == F_Passed ? "  ✓ Passed" : $"  - {FunnelLabels[i]}";
            Console.WriteLine($"  {prefix,-44}: {_funnel[i],8:N0}  ({pct,5:F2}%)");
        }

        // Reset for the next chunk.
        for (int i = 0; i < _funnel.Length; i++) _funnel[i] = 0;
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

        if (intraday is null || intraday.Count == 0) return Drop(F_NoIntraday, out signal);
        if (daily is null || daily.Count < _config.MinHistoricalDays) return Drop(F_NoDaily, out signal);

        // Day-of-week skip (cheap rejection — fastest possible drop).
        // Empirical: Tuesday is consistently the worst day for this strategy.
        if (_config.SkipTuesday && intraday.Last().Timestamp.Date.DayOfWeek == DayOfWeek.Tuesday)
            return Drop(F_SkippedDay, out signal);

        // Futures are only required when OI confluence is enabled. Without
        // it the strategy degrades cleanly to plain RVOL+ORB.
        var needFutures = _config.RequireOiConfluence;
        if (needFutures && (futures is null || futures.Count == 0))
            return Drop(F_NoFutures, out signal);

        var currentDay = intraday.Last().Timestamp.Date;
        var currentIntraday = intraday.Where(c => c.Timestamp.Date == currentDay).ToList();
        if (currentIntraday.Count == 0) return Drop(F_NoIntraday, out signal);

        var futToday = needFutures
            ? futures!.Where(c => c.Timestamp.Date == currentDay).ToList()
            : new List<Candle>();
        if (needFutures && futToday.Count == 0) return Drop(F_NoFutures, out signal);

        // ── 2. Build cash OR window ─────────────────────────────────────
        var orWindowStart = currentIntraday.First().Timestamp;
        var orWindowEnd   = orWindowStart.AddMinutes(_config.OpeningRangeMinutes);

        var orCandles = currentIntraday.Where(c => c.Timestamp < orWindowEnd).ToList();
        if (orCandles.Count == 0) return Drop(F_OrDoji, out signal);

        var orOpen   = orCandles.First().Open;
        var orClose  = orCandles.Last().Close;
        var orHigh   = orCandles.Max(c => c.High);
        var orLow    = orCandles.Min(c => c.Low);
        var orVolume = orCandles.Sum(c => c.Volume);
        var orRange  = orHigh - orLow;

        // Doji rejection: |close - open| / range must exceed the threshold.
        if (orRange <= 0) return Drop(F_OrDoji, out signal);
        var bodyFraction = Math.Abs(orClose - orOpen) / orRange;
        if (bodyFraction < _config.DojiThreshold) return Drop(F_OrDoji, out signal);

        // ── 3. Min price (cheap, from OR open) ──────────────────────────
        if (orOpen < _config.MinPrice) return Drop(F_MinPrice, out signal);

        // ── 4. Yesterday range % (cheap, from last daily candle) ────────
        var sortedDaily = daily.OrderBy(c => c.Timestamp).ToList();
        var prevDay = sortedDaily[^1];
        if (prevDay.Close <= 0) return Drop(F_YdayRange, out signal);
        var prevRangePct = (prevDay.High - prevDay.Low) / prevDay.Close * 100m;
        if (prevRangePct >= _config.MaxYesterdayRangePct) return Drop(F_YdayRange, out signal);

        // ── 4b. Gap filter (Phase 9J — the BIG one) ─────────────────────
        // Compute gap %, reject if not satisfying MinGapPct.
        // Empirically: only gap-downs ≥1.5% have meaningful edge.
        var earlyGapPct = (orOpen - prevDay.Close) / prevDay.Close * 100m;
        if (earlyGapPct > _config.MinGapPct) return Drop(F_GapNotDown, out signal);

        // ── 5. ATR(14) ≥ MinAtrPercent ──────────────────────────────────
        var atr = ComputeAtrWilder(sortedDaily, _config.AtrLookback);
        if (atr <= 0) return Drop(F_AtrPct, out signal);
        var atrPct = atr / orOpen * 100m;
        if (atrPct < _config.MinAtrPercent) return Drop(F_AtrPct, out signal);

        // ── 6. Min avg daily rupee turnover ─────────────────────────────
        var rupeeVolDays = Math.Min(30, sortedDaily.Count);
        var avgRupeeVol = sortedDaily
            .OrderByDescending(c => c.Timestamp)
            .Take(rupeeVolDays)
            .Average(c => (double)c.Volume * (double)c.Close);
        if (avgRupeeVol < _config.MinAvgRupeeVolume) return Drop(F_RupeeVol, out signal);

        // ── 7. OR direction = green (long-only v1) ──────────────────────
        // Spec §5.4: skip red OR + any long-side confluence as a conflict.
        // Short-side OR (red) trades require Trade.Direction migration, deferred.
        if (orClose <= orOpen) return Drop(F_OrRed, out signal);

        // ── 8. RVOL_15min: today's OR volume vs same-slot history ───────
        var sameSlotVolumes = ComputeSameSlotVolumes(
            intraday, currentDay, _config.OpeningRangeMinutes, _config.RvolLookbackDays);
        if (sameSlotVolumes.Count == 0) return Drop(F_RvolFloor, out signal);
        var avgSlotVol = sameSlotVolumes.Average();
        if (avgSlotVol <= 0) return Drop(F_RvolFloor, out signal);
        var rvol = (decimal)orVolume / (decimal)avgSlotVol;
        if (rvol < _config.MinRvol) return Drop(F_RvolFloor, out signal);

        // ── 9. Futures OR + OI delta (only when confluence is on) ──────
        decimal confluenceW;

        if (_config.RequireOiConfluence)
        {
            var futOrCandles = futToday.Where(c => c.Timestamp < orWindowEnd).ToList();
            if (futOrCandles.Count == 0) return Drop(F_OiUnavailable, out signal);
            var futOrOpen  = futOrCandles.First().Open;
            var futOrClose = futOrCandles.Last().Close;

            var oiStart = futOrCandles.First().OpenInterest ?? 0;
            var oiEnd   = futOrCandles.Last().OpenInterest  ?? 0;
            if (oiStart <= 0) return Drop(F_OiUnavailable, out signal);

            var oiDeltaPct = (decimal)(oiEnd - oiStart) / oiStart * 100m;
            var absOiDeltaPct = Math.Abs(oiDeltaPct);

            // ── 10. Confluence cell mapping (long-only v1) ──────────────
            // Flat OI — drop entirely per spec §5.3
            if (absOiDeltaPct < _config.MinOiDeltaPercent) return Drop(F_OiDeltaFlat, out signal);

            var futPriceDir = futOrClose > futOrOpen ? 1 : (futOrClose < futOrOpen ? -1 : 0);
            var oiDir       = oiDeltaPct > 0 ? 1 : -1;

            // We already filtered to green-OR above (long-only). Map only
            // the long-side cells:
            if (futPriceDir == 1 && oiDir == 1)
                confluenceW = 1.0m;          // Long buildup → full size
            else if (futPriceDir == 1 && oiDir == -1)
                confluenceW = 0.5m;          // Short covering → half size
            else
                return Drop(F_OiConflict, out signal);   // Conflicting or short-side cell
        }
        else
        {
            // Plain RVOL+ORB variant (toggle for §11.4 fallback) — no OI weighting.
            confluenceW = 1.0m;
        }

        // ── 11. Composite score threshold ───────────────────────────────
        var score = rvol * confluenceW;
        if (score < _config.MinScoreThreshold) return Drop(F_ScoreLow, out signal);

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

        // Phase 9I rich context for post-hoc analysis.
        var orWidthPct = orOpen > 0 ? (orHigh - orLow) / orOpen * 100m : 0m;
        // earlyGapPct was already computed at step 4b — reuse.

        signal = new ScreenerSignal(
            Candles: new List<Candle> { orSummary },
            SizingMultiplier: confluenceW,
            Atr: atr,
            RvolAtEntry: rvol,
            OrWidthPct: orWidthPct,
            GapPct: earlyGapPct);
        _funnel[F_Passed]++;
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
    /// `orMinutes` of that day. Returns the most-recent `days` such values.
    ///
    /// Sort groups by date DESCENDING before taking N — earlier versions
    /// used `OrderByDescending(_ => 0)` which is a no-op (sorting by a
    /// constant key) and meant Take(N) was pulling the oldest N days of
    /// the history window instead of the newest. That biased RVOL against
    /// a stale baseline.
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
            .OrderByDescending(g => g.Key)   // most recent trading days first
            .Take(days)
            .Select(g =>
            {
                var ordered = g.OrderBy(c => c.Timestamp).ToList();
                if (ordered.Count == 0) return 0.0;
                var firstTs = ordered[0].Timestamp;
                var cutoff  = firstTs.AddMinutes(orMinutes);
                return ordered.Where(c => c.Timestamp < cutoff).Sum(c => (double)c.Volume);
            })
            .Where(v => v > 0)
            .ToList();
    }
}
