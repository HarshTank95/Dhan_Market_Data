using DhanMarketData.Core.Diagnostics;
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
        var ok = Scan(allCandles, log: null, out var r);
        signalCandles = ok ? new List<Candle> { r.Trigger! } : new List<Candle>();
        return ok;
    }

    public bool MeetsConditions(ScreenerContext context, out List<Candle> signalCandles)
    {
        var ok = Scan(context.Intraday, context.Decisions, out var r);
        signalCandles = ok ? new List<Candle> { r.Trigger! } : new List<Candle>();
        return ok;
    }

    public bool MeetsSignal(ScreenerContext context, out ScreenerSignal signal)
    {
        var ok = Scan(context.Intraday, context.Decisions, out var r);
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

    private bool Scan(List<Candle> intraday, ScreenDecisionRecorder? log, out ScanResult result)
    {
        result = default;
        if (intraday == null || intraday.Count == 0)
        { log?.Reject("no_intraday", "no intraday candles"); return false; }

        var sorted = intraday.OrderBy(c => c.Timestamp).ToList();
        var currentDay = sorted[^1].Timestamp.Date;

        // ── F1. Day-of-week filter (expiry-day avoidance) ────────────
        if (!IsDayAllowed(currentDay.DayOfWeek))
        { log?.Reject("day_not_allowed", $"{currentDay.DayOfWeek} not in the allowed-days set"); return false; }

        // Per-day groups.
        var byDay = sorted.GroupBy(c => c.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Timestamp).ToList());
        if (!byDay.TryGetValue(currentDay, out var today))
        { log?.Reject("no_current_day", "no intraday candles for the current day"); return false; }
        if (today.Count < _config.OpeningRangeBars + 2)
        { log?.Reject("insufficient_bars", $"today bars={today.Count} < required {_config.OpeningRangeBars + 2}"); return false; }

        // ── F2. Liquidity (prior-day average volume) + gap baseline ──
        var priorDays = byDay.Keys.Where(d => d < currentDay)
            .OrderByDescending(d => d).Take(_config.VolumeLookbackDays).ToList();
        if (priorDays.Count == 0)
        { log?.Reject("no_prior_days", "no prior-day history for the volume baseline"); return false; }
        double avgDailyVol = priorDays.Average(d => byDay[d].Sum(c => (double)c.Volume));
        if (avgDailyVol < _config.MinAverageDailyVolume)
        { log?.Reject("liquidity", $"avg daily vol {avgDailyVol:N0} < min {_config.MinAverageDailyVolume:N0}", today[0].Open); return false; }

        // ── F3. Gap (momentum-day confirmation) ──────────────────────
        var prevDay = priorDays[0];                       // most-recent prior day
        var prevClose = byDay[prevDay][^1].Close;
        var gapPct = prevClose > 0 ? (today[0].Open - prevClose) / prevClose * 100m : 0m;
        if (gapPct < _config.MinGapPct)
        { log?.Reject("gap_below_min", $"gap {gapPct:F2}% < min {_config.MinGapPct:F2}%", today[0].Open); return false; }

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
        if (orHigh <= 0)
        { log?.Reject("bad_or", "opening-range high ≤ 0"); return false; }
        var orWidthPct = (orHigh - orLow) / orHigh * 100m;
        if (orWidthPct < _config.MinOrWidthPct)
        { log?.Reject("or_too_narrow", $"OR width {orWidthPct:F2}% < min {_config.MinOrWidthPct:F2}%", today[0].Open); return false; }

        var startUtc = IstToUtc(_config.WindowStart);
        var endUtc = IstToUtc(_config.WindowEnd);
        var avgVolM = (decimal)(avgDailyVol / 1_000_000.0);

        // ── F4. Per-candle breakout scan (first qualifying wins) ─────
        // A `continue` is NOT terminal — a later bar may still trigger — so we
        // record the FURTHEST stage any bar reached via Note(rank,…). On loop
        // fallthrough that becomes the drop reason, showing how close the stock
        // got (e.g. "broke OR but never held a rising VWAP"). Pure side-channel.
        for (int k = orEnd; k < n; k++)
        {
            var c = today[k];

            var t = c.Timestamp.TimeOfDay;
            if (t < startUtc || t > endUtc) continue;

            // (a) fresh break above OR high (prior bar at/below it)
            if (!((double)c.Close > (double)orHigh && (double)today[k - 1].Close <= (double)orHigh)) { log?.Note(1, "no_or_break", $"no fresh break above OR high ₹{orHigh:F2}", c.Close); continue; }

            // (b) above VWAP, slope in [Min, Max] bps
            if ((double)c.Close <= vwap[k]) { log?.Note(2, "below_vwap", $"close ₹{c.Close:F2} ≤ VWAP ₹{vwap[k]:F2} on the break", c.Close); continue; }
            int slb = Math.Max(0, k - _config.VwapSlopeLookback);
            double slopeBps = vwap[slb] > 0 ? (vwap[k] - vwap[slb]) / vwap[slb] * 10000.0 : 0;
            if (slopeBps < (double)_config.MinVwapSlopeBps) { log?.Note(3, "vwap_slope_low", $"VWAP slope {slopeBps:F1} bps < min {_config.MinVwapSlopeBps:F1}", c.Close); continue; }
            if (_config.MaxVwapSlopeBps > 0 && slopeBps >= (double)_config.MaxVwapSlopeBps) { log?.Note(3, "vwap_slope_high", $"VWAP slope {slopeBps:F1} bps ≥ max {_config.MaxVwapSlopeBps:F1} (exhausted)", c.Close); continue; }

            // price floor
            if (c.Close < _config.MinPrice) { log?.Note(4, "min_price", $"close ₹{c.Close:F2} < min ₹{_config.MinPrice:F2}", c.Close); continue; }

            // (d) stop-distance band: stop = min(VWAP, breakout-bar low), entry = next open
            if (k + 1 >= n) break;                         // need a next bar to enter
            var entry = today[k + 1].Open;
            var stop = Math.Min((decimal)vwap[k], c.Low);
            var risk = entry - stop;
            if (entry <= 0 || risk <= 0) { log?.Note(5, "bad_risk", "non-positive risk (entry ≤ stop)", c.Close); continue; }
            var stopDistPct = risk / entry * 100m;
            if (_config.MinStopDistancePct > 0 && stopDistPct < _config.MinStopDistancePct) { log?.Note(5, "stop_too_tight", $"stop {stopDistPct:F2}% < min {_config.MinStopDistancePct:F2}%", c.Close); continue; }
            if (_config.MaxStopDistancePct > 0 && stopDistPct > _config.MaxStopDistancePct) { log?.Note(5, "stop_too_wide", $"stop {stopDistPct:F2}% > max {_config.MaxStopDistancePct:F2}%", c.Close); continue; }

            result = new ScanResult(c, avgVolM, (decimal)slopeBps, orWidthPct);
            return true;
        }

        // No bar triggered. Leave a generic reason only if no Note fired.
        if (log is not null && !log.HasReason)
            log.Reject("no_trigger", "no intraday bar reached the breakout conditions");
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
