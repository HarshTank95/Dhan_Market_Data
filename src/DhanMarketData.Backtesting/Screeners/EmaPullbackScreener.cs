using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// EMA Pullback Continuation Screener.
///
/// "The filters ARE the strategy; the EMA pullback is just the trigger."
/// Successful intraday EMA traders apply it only to stocks that are IN PLAY
/// that day (high relative volume) and genuinely TRENDING (high ADX) — not to
/// every stock. This screener encodes that selection.
///
/// Filter chain (cheap → expensive, early-out):
///  1. Daily history + liquidity + min price
///  2. Daily ATR % floor
///  3. Daily trend band: price 2-10% above daily SMA20 (with-trend only)
///  4. Optional gap band (2-5%)
///  5. Per intraday candle in a time window:
///       a. 9 EMA > 20 EMA, 20 EMA rising, EMA distance in ATR band
///       b. pullback touches 9 EMA, bullish close above it, optional engulfing
///       c. stop-distance band (trigger low)
///       d. ADX ≥ MinAdx           (trend-strength / anti-chop regime filter)
///       e. RVOL ≥ MinRvol         (stock is "in play")
///       f. trigger volume ≥ Mult × recent average (expansion confirmation)
///  6. First qualifying candle is the trigger.
///
/// Per-trade diagnostics carried on the signal (remapped to the generic
/// context columns — see MeetsSignal):
///   RvolAtEntry ← relative volume   OrWidthPct ← ADX   GapPct ← gap %
/// </summary>
public class EmaPullbackScreener : IScreener
{
    private readonly EmaPullbackScreenerConfig _config;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "EMA Gap-Down Reclaim (Long)";
    public string Description =>
        "Buy-the-dip: uptrending stocks (2–10% above their 20-day SMA) that gapped " +
        "down ≥1.5%, where price reclaims the 9-EMA intraday. Gap-down-in-uptrend is " +
        "the selection; the 9-EMA reclaim is the trigger.";

    public int RequiredHistoricalDays => _config.MinHistoricalDays;
    public bool RequiresDailyCandles => true;

    public EmaPullbackScreener(EmaPullbackScreenerConfig? config = null)
    {
        _config = config ?? new EmaPullbackScreenerConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        return false;
    }

    public bool MeetsConditions(ScreenerContext context, out List<Candle> signalCandles)
    {
        var ok = Scan(context, out var r);
        signalCandles = ok ? new List<Candle> { r.Trigger! } : new List<Candle>();
        return ok;
    }

    public bool MeetsSignal(ScreenerContext context, out ScreenerSignal signal)
    {
        var ok = Scan(context, out var r);
        if (!ok)
        {
            signal = new ScreenerSignal(new List<Candle>(), 1.0m);
            return false;
        }
        signal = new ScreenerSignal(
            Candles: new List<Candle> { r.Trigger! },
            SizingMultiplier: 1.0m,
            Atr: r.IntradayAtr,
            RvolAtEntry: r.Rvol,    // relative volume
            OrWidthPct: r.Adx,      // ADX (trend strength)
            GapPct: r.GapPct);      // gap %
        return true;
    }

    private readonly record struct ScanResult(
        Candle? Trigger, decimal IntradayAtr, decimal Rvol, decimal Adx, decimal GapPct);

    private bool Scan(ScreenerContext context, out ScanResult result)
    {
        result = default;
        var log = context.Decisions; // null unless diagnostic logging is on — pure side-channel

        var intraday = context.Intraday;
        var daily = context.Daily;
        if (intraday == null || intraday.Count == 0)
        { log?.Reject("no_intraday", "no intraday candles"); return false; }
        if (daily == null || daily.Count < _config.MinHistoricalDays)
        { log?.Reject("min_history", $"daily candles={daily?.Count ?? 0} < required {_config.MinHistoricalDays}"); return false; }

        var minIntraday = Math.Max(_config.SlowEmaPeriod, _config.IntradayAtrPeriod) + _config.SlopeLookback + 1;
        if (intraday.Count < minIntraday)
        { log?.Reject("insufficient_intraday", $"intraday candles={intraday.Count} < required {minIntraday}"); return false; }

        var sortedIntraday = intraday.OrderBy(c => c.Timestamp).ToList();
        var currentDay = sortedIntraday[^1].Timestamp.Date;

        // ── 1. Liquidity / price ─────────────────────────────────────
        var sortedDaily = daily.OrderBy(c => c.Timestamp).ToList();
        var prevDay = sortedDaily[^1];
        if (prevDay.Close < _config.MinPrice)
        { log?.Reject("min_price", $"prev close ₹{prevDay.Close:F2} < min ₹{_config.MinPrice:F2}", prevDay.Close); return false; }

        var dailyAvgVolDays = Math.Min(20, sortedDaily.Count);
        var avgDailyVol = sortedDaily.OrderByDescending(c => c.Timestamp)
            .Take(dailyAvgVolDays).Average(c => (double)c.Volume);
        if (avgDailyVol < _config.MinAverageDailyVolume)
        { log?.Reject("liquidity", $"avg daily vol {avgDailyVol:N0} < min {_config.MinAverageDailyVolume:N0}", prevDay.Close); return false; }

        // ── 2. Daily ATR % floor ─────────────────────────────────────
        var dailyAtr = ComputeAtrWilder(sortedDaily, _config.DailyAtrPeriod);
        if (dailyAtr <= 0)
        { log?.Reject("atr_unavailable", "daily ATR ≤ 0 (insufficient history)"); return false; }
        if ((dailyAtr / prevDay.Close) * 100m < _config.MinDailyAtrPct)
        { log?.Reject("daily_atr_pct", $"daily ATR {(dailyAtr / prevDay.Close) * 100m:F2}% < min {_config.MinDailyAtrPct:F2}%", prevDay.Close); return false; }

        // ── 3. Daily trend band ──────────────────────────────────────
        var smaPeriod = Math.Min(_config.DailyTrendSmaPeriod, sortedDaily.Count);
        var dailySma = sortedDaily.OrderByDescending(c => c.Timestamp).Take(smaPeriod).Average(c => c.Close);
        var dailyTrendPct = dailySma > 0 ? (prevDay.Close - dailySma) / prevDay.Close * 100m : 0m;
        if (dailyTrendPct < _config.MinDailyTrendPct)
        { log?.Reject("trend_too_weak", $"price {dailyTrendPct:F2}% above SMA{smaPeriod} < min {_config.MinDailyTrendPct:F2}%", prevDay.Close); return false; }
        if (_config.MaxDailyTrendPct > 0 && dailyTrendPct > _config.MaxDailyTrendPct)
        { log?.Reject("trend_too_extended", $"price {dailyTrendPct:F2}% above SMA{smaPeriod} > max {_config.MaxDailyTrendPct:F2}%", prevDay.Close); return false; }

        // Per-day candle groups (for RVOL same-time baseline).
        var byDay = sortedIntraday.GroupBy(c => c.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Timestamp).ToList());
        var todayCandles = byDay[currentDay];
        var firstTodayIdx = sortedIntraday.FindIndex(c => c.Timestamp.Date == currentDay);
        if (todayCandles.Count == 0 || firstTodayIdx < 0)
        { log?.Reject("no_current_day", "no intraday candles for the current day"); return false; }

        // ── 4. Gap (optional) ────────────────────────────────────────
        var gapPct = prevDay.Close > 0 ? (todayCandles[0].Open - prevDay.Close) / prevDay.Close * 100m : 0m;
        if (_config.MinGapPct > 0)
        {
            if (gapPct < _config.MinGapPct)
            { log?.Reject("gap_below_min", $"gap {gapPct:F2}% < min {_config.MinGapPct:F2}%", todayCandles[0].Open); return false; }
            if (_config.MaxGapPct > 0 && gapPct > _config.MaxGapPct)
            { log?.Reject("gap_above_max", $"gap {gapPct:F2}% > max {_config.MaxGapPct:F2}%", todayCandles[0].Open); return false; }
        }
        // Buy-the-dip selection (Run #83): require today's gap ≤ MaxEntryGapPct.
        // The edge concentrates in uptrending stocks that gapped DOWN.
        if (gapPct > _config.MaxEntryGapPct)
        { log?.Reject("gap_not_down", $"gap {gapPct:F2}% > max entry gap {_config.MaxEntryGapPct:F2}% (need gap-down)", todayCandles[0].Open); return false; }

        // ── Pre-compute aligned indicator series ─────────────────────
        var fastEma = ComputeEma(sortedIntraday, _config.FastEmaPeriod);
        var slowEma = ComputeEma(sortedIntraday, _config.SlowEmaPeriod);
        var atrSeries = ComputeIntradayAtrSeries(sortedIntraday, _config.IntradayAtrPeriod);
        var adxSeries = ComputeAdxSeries(sortedIntraday, _config.AdxPeriod);

        var morningStartUtc = IstToUtc(_config.MorningStart);
        var morningEndUtc = IstToUtc(_config.MorningEnd);
        var afternoonStartUtc = IstToUtc(_config.AfternoonStart);
        var afternoonEndUtc = IstToUtc(_config.AfternoonEnd);

        var firstValidIdx = Math.Max(
            Math.Max(_config.SlowEmaPeriod + _config.SlopeLookback, _config.IntradayAtrPeriod + 1),
            2 * _config.AdxPeriod);

        var priorDays = byDay.Keys.Where(d => d < currentDay).OrderByDescending(d => d)
            .Take(_config.RvolLookbackDays).ToList();

        // Per-candle trigger scan. A `continue` is NOT terminal (a later candle
        // may still trigger), so we record the FURTHEST stage any candle reached
        // via Note(rank,…); on loop fallthrough that becomes the drop reason —
        // telling us how close the stock got to a signal. Ranks increase with
        // filter depth. Pure side-channel; control flow is unchanged.
        for (int i = 0; i < sortedIntraday.Count; i++)
        {
            var c = sortedIntraday[i];
            if (c.Timestamp.Date != currentDay) continue;
            if (i < firstValidIdx || i == 0) continue;

            var t = c.Timestamp.TimeOfDay;
            var inMorning = t >= morningStartUtc && t <= morningEndUtc;
            var inAfternoon = t >= afternoonStartUtc && t <= afternoonEndUtc;
            if (!inMorning && !inAfternoon) { log?.Note(0, "outside_window", "no candle in the entry time window", c.Close); continue; }

            // (a) trend stack + slope + distance
            if (fastEma[i] <= slowEma[i]) { log?.Note(1, "ema_not_stacked", "9-EMA ≤ 20-EMA (no uptrend stack)", c.Close); continue; }
            var slopeFromIdx = i - _config.SlopeLookback;
            if (slopeFromIdx < 0 || slowEma[i] <= slowEma[slopeFromIdx]) { log?.Note(2, "slow_ema_not_rising", "20-EMA not rising over slope lookback", c.Close); continue; }
            var atr = atrSeries[i];
            if (atr <= 0) { log?.Note(2, "intraday_atr_zero", "intraday ATR ≤ 0", c.Close); continue; }
            var emaDistAtr = (fastEma[i] - slowEma[i]) / atr;
            if (emaDistAtr < _config.MinEmaDistanceAtr || emaDistAtr > _config.MaxEmaDistanceAtr) { log?.Note(3, "ema_distance_band", $"EMA gap {emaDistAtr:F2} ATR outside [{_config.MinEmaDistanceAtr:F2}, {_config.MaxEmaDistanceAtr:F2}]", c.Close); continue; }

            // (b) pullback touch + bullish close above
            if (c.Low > fastEma[i] || c.High < fastEma[i]) { log?.Note(4, "no_pullback_touch", "candle did not touch the 9-EMA", c.Close); continue; }
            if (c.Close <= fastEma[i]) { log?.Note(5, "close_below_ema", "close ≤ 9-EMA (no reclaim)", c.Close); continue; }
            if (c.Close <= c.Open) { log?.Note(5, "not_bullish", "candle not bullish (close ≤ open)", c.Close); continue; }
            if (_config.RequireEngulfing && c.Close <= sortedIntraday[i - 1].High) { log?.Note(6, "not_engulfing", "close did not engulf prior bar high", c.Close); continue; }

            // (c) stop-distance band
            var stopDist = c.Close - c.Low;
            if (stopDist <= 0) { log?.Note(6, "bad_stop_distance", "non-positive stop distance", c.Close); continue; }
            var stopDistPct = stopDist / c.Close * 100m;
            if (_config.MinStopDistancePct > 0 && stopDistPct < _config.MinStopDistancePct) { log?.Note(7, "stop_too_tight", $"stop {stopDistPct:F2}% < min {_config.MinStopDistancePct:F2}%", c.Close); continue; }
            if (_config.MaxStopDistancePct > 0 && stopDistPct > _config.MaxStopDistancePct) { log?.Note(7, "stop_too_wide", $"stop {stopDistPct:F2}% > max {_config.MaxStopDistancePct:F2}%", c.Close); continue; }

            // (d) ADX regime filter — skip chop (min) and exhausted/extended moves (max)
            var adx = adxSeries[i];
            if (_config.MinAdx > 0 && adx < _config.MinAdx) { log?.Note(8, "adx_too_low", $"ADX {adx:F1} < min {_config.MinAdx:F1} (chop)", c.Close); continue; }
            if (_config.MaxAdx > 0 && adx > _config.MaxAdx) { log?.Note(8, "adx_too_high", $"ADX {adx:F1} > max {_config.MaxAdx:F1} (exhausted)", c.Close); continue; }

            // (e) RVOL — stock must be "in play"
            var rvol = ComputeRvol(byDay, priorDays, todayCandles, i - firstTodayIdx);
            if (_config.MinRvol > 0 && rvol < _config.MinRvol) { log?.Note(9, "rvol_too_low", $"RVOL {rvol:F2} < min {_config.MinRvol:F2} (not in play)", c.Close); continue; }

            // (f) trigger volume expansion
            if (_config.MinTriggerVolMult > 0)
            {
                var volMult = ComputeTriggerVolMult(sortedIntraday, i, 20);
                if (volMult < _config.MinTriggerVolMult) { log?.Note(10, "trigger_vol_weak", $"trigger vol {volMult:F2}× < min {_config.MinTriggerVolMult:F2}×", c.Close); continue; }
            }

            result = new ScanResult(c, atr, rvol, adx, gapPct);
            return true;
        }

        // No candle triggered. If logging is on and no per-candle Note was
        // recorded (e.g. zero candles in range), leave a generic reason.
        if (log is not null && !log.HasReason)
            log.Reject("no_trigger", "no intraday candle reached the trigger conditions");
        return false;
    }

    // ── RVOL: today's cumulative volume to candle k ÷ same-k average over prior days ──
    private static decimal ComputeRvol(
        Dictionary<DateTime, List<Candle>> byDay, List<DateTime> priorDays,
        List<Candle> todayCandles, int k)
    {
        if (k < 0) k = 0;
        long todayCum = 0;
        for (int j = 0; j <= k && j < todayCandles.Count; j++) todayCum += todayCandles[j].Volume;
        if (todayCum <= 0 || priorDays.Count == 0) return 0m;

        double sum = 0; int n = 0;
        foreach (var d in priorDays)
        {
            var dc = byDay[d];
            long cum = 0;
            for (int j = 0; j <= k && j < dc.Count; j++) cum += dc[j].Volume;
            if (cum > 0) { sum += cum; n++; }
        }
        if (n == 0) return 0m;
        var avgPrior = sum / n;
        return avgPrior > 0 ? (decimal)(todayCum / avgPrior) : 0m;
    }

    private static decimal ComputeTriggerVolMult(List<Candle> sorted, int i, int lookback)
    {
        int from = Math.Max(0, i - lookback);
        long sum = 0; int n = 0;
        for (int j = from; j < i; j++) { sum += sorted[j].Volume; n++; }
        if (n == 0) return 0m;
        var avg = (double)sum / n;
        return avg > 0 ? (decimal)(sorted[i].Volume / avg) : 0m;
    }

    private static decimal[] ComputeEma(List<Candle> sorted, int period)
    {
        var ema = new decimal[sorted.Count];
        if (sorted.Count < period) return ema;
        decimal sum = 0;
        for (int i = 0; i < period; i++) sum += sorted[i].Close;
        ema[period - 1] = sum / period;
        decimal alpha = 2m / (period + 1);
        for (int i = period; i < sorted.Count; i++)
            ema[i] = sorted[i].Close * alpha + ema[i - 1] * (1m - alpha);
        return ema;
    }

    private static decimal[] ComputeIntradayAtrSeries(List<Candle> sorted, int period)
    {
        var atr = new decimal[sorted.Count];
        if (sorted.Count <= period) return atr;
        var trs = new decimal[sorted.Count];
        for (int i = 1; i < sorted.Count; i++)
        {
            var c = sorted[i]; var pc = sorted[i - 1].Close;
            trs[i] = Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - pc), Math.Abs(c.Low - pc)));
        }
        decimal sumTr = 0;
        for (int i = 1; i <= period; i++) sumTr += trs[i];
        atr[period] = sumTr / period;
        for (int i = period + 1; i < sorted.Count; i++)
            atr[i] = ((atr[i - 1] * (period - 1)) + trs[i]) / period;
        return atr;
    }

    // Wilder ADX series aligned with input. Values valid from ~2×period onward.
    private static decimal[] ComputeAdxSeries(List<Candle> sorted, int period)
    {
        int len = sorted.Count;
        var adx = new decimal[len];
        if (len <= 2 * period) return adx;

        var plusDM = new decimal[len];
        var minusDM = new decimal[len];
        var tr = new decimal[len];
        for (int i = 1; i < len; i++)
        {
            var up = sorted[i].High - sorted[i - 1].High;
            var down = sorted[i - 1].Low - sorted[i].Low;
            plusDM[i] = (up > down && up > 0) ? up : 0m;
            minusDM[i] = (down > up && down > 0) ? down : 0m;
            var pc = sorted[i - 1].Close;
            tr[i] = Math.Max(sorted[i].High - sorted[i].Low,
                    Math.Max(Math.Abs(sorted[i].High - pc), Math.Abs(sorted[i].Low - pc)));
        }

        // Wilder-smoothed +DM, -DM, TR (seed = sum of first `period`, starting at index 1).
        decimal sPlus = 0, sMinus = 0, sTr = 0;
        for (int i = 1; i <= period; i++) { sPlus += plusDM[i]; sMinus += minusDM[i]; sTr += tr[i]; }

        var dx = new decimal[len];
        for (int i = period + 1; i < len; i++)
        {
            sPlus = sPlus - (sPlus / period) + plusDM[i];
            sMinus = sMinus - (sMinus / period) + minusDM[i];
            sTr = sTr - (sTr / period) + tr[i];
            if (sTr <= 0) { dx[i] = 0; continue; }
            var plusDI = 100m * sPlus / sTr;
            var minusDI = 100m * sMinus / sTr;
            var diSum = plusDI + minusDI;
            dx[i] = diSum > 0 ? 100m * Math.Abs(plusDI - minusDI) / diSum : 0m;
        }

        // ADX = Wilder RMA of DX. Seed = average of first `period` DX values (indices period+1 .. 2*period).
        int seedStart = period + 1, seedEnd = 2 * period;
        if (seedEnd >= len) return adx;
        decimal dxSum = 0;
        for (int i = seedStart; i <= seedEnd; i++) dxSum += dx[i];
        adx[seedEnd] = dxSum / period;
        for (int i = seedEnd + 1; i < len; i++)
            adx[i] = ((adx[i - 1] * (period - 1)) + dx[i]) / period;
        return adx;
    }

    private static decimal ComputeAtrWilder(List<Candle> sortedDaily, int period)
    {
        if (sortedDaily == null || sortedDaily.Count <= period) return 0;
        var trs = new List<decimal>(sortedDaily.Count - 1);
        for (int i = 1; i < sortedDaily.Count; i++)
        {
            var c = sortedDaily[i]; var pc = sortedDaily[i - 1].Close;
            trs.Add(Math.Max(c.High - c.Low, Math.Max(Math.Abs(c.High - pc), Math.Abs(c.Low - pc))));
        }
        if (trs.Count < period) return 0;
        decimal atr = trs.Take(period).Average();
        for (int i = period; i < trs.Count; i++) atr = ((atr * (period - 1)) + trs[i]) / period;
        return atr;
    }

    private static TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utc = istTime - IstOffset;
        return utc < TimeSpan.Zero ? utc + TimeSpan.FromDays(1) : utc;
    }
}
