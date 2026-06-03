using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// VWAP Opening-Range Breakout (Long) Screener.
///
/// Momentum: on a trending day (Mon/Wed, liquid, priced ≥₹500, gap ≥ 0, wide opening
/// range), price breaks above the opening-range high while holding above a RISING
/// session VWAP whose slope is in a "with-flow but not exhausted" band. The selection
/// (day + liquidity + OR-width + slope band + gap) is the edge; the OR break is the
/// trigger.
///
/// Intraday-only — session VWAP from the current day; liquidity + gap from the
/// prior-day intraday history the orchestrator pre-rolls. No daily candles / token.
///
/// Diagnostics carried on the signal (remapped to the generic context columns — see
/// MeetsSignal): RvolAtEntry ← prior avg daily volume (M shares), OrWidthPct ←
/// VWAP slope (bps), GapPct ← opening-range width %.
/// </summary>
public class VwapOrbScreener : IScreener
{
    private readonly VwapOrbScreenerConfig _config;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "VWAP ORB Momentum (Long)";
    public string Description =>
        "Momentum: a liquid (≥30L/day), higher-priced (≥₹500) stock on a trending Mon/Wed " +
        "session breaks above its opening-range high while holding above a rising VWAP " +
        "(slope 20–50 bps), on a non-negative gap day. Held to the close. Selection is the edge.";

    public int RequiredHistoricalDays => _config.MinHistoricalDays;
    public bool RequiresDailyCandles => false;

    public VwapOrbScreener(VwapOrbScreenerConfig? config = null)
    {
        _config = config ?? new VwapOrbScreenerConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        var ok = Scan(allCandles, out var r);
        signalCandles = ok ? new List<Candle> { r.Trigger! } : new List<Candle>();
        return ok;
    }

    public bool MeetsConditions(ScreenerContext context, out List<Candle> signalCandles)
        => MeetsConditions(context.Intraday, out signalCandles);

    public bool MeetsSignal(ScreenerContext context, out ScreenerSignal signal)
    {
        var ok = Scan(context.Intraday, out var r);
        if (!ok)
        {
            signal = new ScreenerSignal(new List<Candle>(), 1.0m);
            return false;
        }
        signal = new ScreenerSignal(
            Candles: new List<Candle> { r.Trigger! },
            SizingMultiplier: 1.0m,
            Atr: 0m,
            RvolAtEntry: r.AvgDailyVolMillions,  // liquidity (M shares)
            OrWidthPct: r.VwapSlopeBps,          // VWAP slope (bps)
            GapPct: r.OrWidthPct);               // opening-range width %
        return true;
    }

    private readonly record struct ScanResult(
        Candle? Trigger, decimal AvgDailyVolMillions, decimal VwapSlopeBps, decimal OrWidthPct);

    private bool Scan(List<Candle> intraday, out ScanResult result)
    {
        result = default;
        if (intraday == null || intraday.Count == 0) return false;

        var sorted = intraday.OrderBy(c => c.Timestamp).ToList();
        var currentDay = sorted[^1].Timestamp.Date;

        // ── F1. Day-of-week filter (expiry-day avoidance) ────────────
        if (!IsDayAllowed(currentDay.DayOfWeek)) return false;

        // Per-day groups.
        var byDay = sorted.GroupBy(c => c.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Timestamp).ToList());
        if (!byDay.TryGetValue(currentDay, out var today)) return false;
        if (today.Count < _config.OpeningRangeBars + 2) return false;

        // ── F2. Liquidity (prior-day average volume) + gap baseline ──
        var priorDays = byDay.Keys.Where(d => d < currentDay)
            .OrderByDescending(d => d).Take(_config.VolumeLookbackDays).ToList();
        if (priorDays.Count == 0) return false;
        double avgDailyVol = priorDays.Average(d => byDay[d].Sum(c => (double)c.Volume));
        if (avgDailyVol < _config.MinAverageDailyVolume) return false;

        // ── F3. Gap (momentum-day confirmation) ──────────────────────
        var prevDay = priorDays[0];                       // most-recent prior day
        var prevClose = byDay[prevDay][^1].Close;
        var gapPct = prevClose > 0 ? (today[0].Open - prevClose) / prevClose * 100m : 0m;
        if (gapPct < _config.MinGapPct) return false;

        // ── Session VWAP over today's candles ────────────────────────
        int n = today.Count;
        var vwap = new double[n];
        double cumPV = 0, cumV = 0;
        for (int k = 0; k < n; k++)
        {
            double tp = ((double)today[k].High + (double)today[k].Low + (double)today[k].Close) / 3.0;
            double v = Math.Max(today[k].Volume, 1);
            cumPV += tp * v; cumV += v;
            vwap[k] = cumPV / cumV;
        }

        // ── Opening range (first OpeningRangeBars bars) ──────────────
        int orEnd = Math.Min(_config.OpeningRangeBars, n - 1);
        decimal orHigh = decimal.MinValue, orLow = decimal.MaxValue;
        for (int k = 0; k < orEnd; k++)
        {
            if (today[k].High > orHigh) orHigh = today[k].High;
            if (today[k].Low < orLow) orLow = today[k].Low;
        }
        if (orHigh <= 0) return false;
        var orWidthPct = (orHigh - orLow) / orHigh * 100m;
        if (orWidthPct < _config.MinOrWidthPct) return false;

        var startUtc = IstToUtc(_config.WindowStart);
        var endUtc = IstToUtc(_config.WindowEnd);
        var avgVolM = (decimal)(avgDailyVol / 1_000_000.0);

        // ── F4. Per-candle breakout scan (first qualifying wins) ─────
        for (int k = orEnd; k < n; k++)
        {
            var c = today[k];

            var t = c.Timestamp.TimeOfDay;
            if (t < startUtc || t > endUtc) continue;

            // (a) fresh break above OR high (prior bar at/below it)
            if (!((double)c.Close > (double)orHigh && (double)today[k - 1].Close <= (double)orHigh)) continue;

            // (b) above VWAP, slope in [Min, Max] bps
            if ((double)c.Close <= vwap[k]) continue;
            int slb = Math.Max(0, k - _config.VwapSlopeLookback);
            double slopeBps = vwap[slb] > 0 ? (vwap[k] - vwap[slb]) / vwap[slb] * 10000.0 : 0;
            if (slopeBps < (double)_config.MinVwapSlopeBps) continue;
            if (_config.MaxVwapSlopeBps > 0 && slopeBps >= (double)_config.MaxVwapSlopeBps) continue;

            // price floor
            if (c.Close < _config.MinPrice) continue;

            // (d) stop-distance band: stop = min(VWAP, breakout-bar low), entry = next open
            if (k + 1 >= n) break;                         // need a next bar to enter
            var entry = today[k + 1].Open;
            var stop = Math.Min((decimal)vwap[k], c.Low);
            var risk = entry - stop;
            if (entry <= 0 || risk <= 0) continue;
            var stopDistPct = risk / entry * 100m;
            if (_config.MinStopDistancePct > 0 && stopDistPct < _config.MinStopDistancePct) continue;
            if (_config.MaxStopDistancePct > 0 && stopDistPct > _config.MaxStopDistancePct) continue;

            result = new ScanResult(c, avgVolM, (decimal)slopeBps, orWidthPct);
            return true;
        }

        return false;
    }

    private bool IsDayAllowed(DayOfWeek d) => d switch
    {
        DayOfWeek.Monday => _config.AllowMon,
        DayOfWeek.Tuesday => _config.AllowTue,
        DayOfWeek.Wednesday => _config.AllowWed,
        DayOfWeek.Thursday => _config.AllowThu,
        DayOfWeek.Friday => _config.AllowFri,
        _ => false,
    };

    private static TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utc = istTime - IstOffset;
        return utc < TimeSpan.Zero ? utc + TimeSpan.FromDays(1) : utc;
    }
}
