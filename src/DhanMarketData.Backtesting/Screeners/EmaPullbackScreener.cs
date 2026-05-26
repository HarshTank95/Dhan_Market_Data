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

        var intraday = context.Intraday;
        var daily = context.Daily;
        if (intraday == null || intraday.Count == 0) return false;
        if (daily == null || daily.Count < _config.MinHistoricalDays) return false;

        var minIntraday = Math.Max(_config.SlowEmaPeriod, _config.IntradayAtrPeriod) + _config.SlopeLookback + 1;
        if (intraday.Count < minIntraday) return false;

        var sortedIntraday = intraday.OrderBy(c => c.Timestamp).ToList();
        var currentDay = sortedIntraday[^1].Timestamp.Date;

        // ── 1. Liquidity / price ─────────────────────────────────────
        var sortedDaily = daily.OrderBy(c => c.Timestamp).ToList();
        var prevDay = sortedDaily[^1];
        if (prevDay.Close < _config.MinPrice) return false;

        var dailyAvgVolDays = Math.Min(20, sortedDaily.Count);
        var avgDailyVol = sortedDaily.OrderByDescending(c => c.Timestamp)
            .Take(dailyAvgVolDays).Average(c => (double)c.Volume);
        if (avgDailyVol < _config.MinAverageDailyVolume) return false;

        // ── 2. Daily ATR % floor ─────────────────────────────────────
        var dailyAtr = ComputeAtrWilder(sortedDaily, _config.DailyAtrPeriod);
        if (dailyAtr <= 0) return false;
        if ((dailyAtr / prevDay.Close) * 100m < _config.MinDailyAtrPct) return false;

        // ── 3. Daily trend band ──────────────────────────────────────
        var smaPeriod = Math.Min(_config.DailyTrendSmaPeriod, sortedDaily.Count);
        var dailySma = sortedDaily.OrderByDescending(c => c.Timestamp).Take(smaPeriod).Average(c => c.Close);
        var dailyTrendPct = dailySma > 0 ? (prevDay.Close - dailySma) / prevDay.Close * 100m : 0m;
        if (dailyTrendPct < _config.MinDailyTrendPct) return false;
        if (_config.MaxDailyTrendPct > 0 && dailyTrendPct > _config.MaxDailyTrendPct) return false;

        // Per-day candle groups (for RVOL same-time baseline).
        var byDay = sortedIntraday.GroupBy(c => c.Timestamp.Date)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Timestamp).ToList());
        var todayCandles = byDay[currentDay];
        var firstTodayIdx = sortedIntraday.FindIndex(c => c.Timestamp.Date == currentDay);
        if (todayCandles.Count == 0 || firstTodayIdx < 0) return false;

        // ── 4. Gap (optional) ────────────────────────────────────────
        var gapPct = prevDay.Close > 0 ? (todayCandles[0].Open - prevDay.Close) / prevDay.Close * 100m : 0m;
        if (_config.MinGapPct > 0)
        {
            if (gapPct < _config.MinGapPct) return false;
            if (_config.MaxGapPct > 0 && gapPct > _config.MaxGapPct) return false;
        }
        // Buy-the-dip selection (Run #83): require today's gap ≤ MaxEntryGapPct.
        // The edge concentrates in uptrending stocks that gapped DOWN.
        if (gapPct > _config.MaxEntryGapPct) return false;

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

        for (int i = 0; i < sortedIntraday.Count; i++)
        {
            var c = sortedIntraday[i];
            if (c.Timestamp.Date != currentDay) continue;
            if (i < firstValidIdx || i == 0) continue;

            var t = c.Timestamp.TimeOfDay;
            var inMorning = t >= morningStartUtc && t <= morningEndUtc;
            var inAfternoon = t >= afternoonStartUtc && t <= afternoonEndUtc;
            if (!inMorning && !inAfternoon) continue;

            // (a) trend stack + slope + distance
            if (fastEma[i] <= slowEma[i]) continue;
            var slopeFromIdx = i - _config.SlopeLookback;
            if (slopeFromIdx < 0 || slowEma[i] <= slowEma[slopeFromIdx]) continue;
            var atr = atrSeries[i];
            if (atr <= 0) continue;
            var emaDistAtr = (fastEma[i] - slowEma[i]) / atr;
            if (emaDistAtr < _config.MinEmaDistanceAtr || emaDistAtr > _config.MaxEmaDistanceAtr) continue;

            // (b) pullback touch + bullish close above
            if (c.Low > fastEma[i] || c.High < fastEma[i]) continue;
            if (c.Close <= fastEma[i]) continue;
            if (c.Close <= c.Open) continue;
            if (_config.RequireEngulfing && c.Close <= sortedIntraday[i - 1].High) continue;

            // (c) stop-distance band
            var stopDist = c.Close - c.Low;
            if (stopDist <= 0) continue;
            var stopDistPct = stopDist / c.Close * 100m;
            if (_config.MinStopDistancePct > 0 && stopDistPct < _config.MinStopDistancePct) continue;
            if (_config.MaxStopDistancePct > 0 && stopDistPct > _config.MaxStopDistancePct) continue;

            // (d) ADX regime filter — skip chop (min) and exhausted/extended moves (max)
            var adx = adxSeries[i];
            if (_config.MinAdx > 0 && adx < _config.MinAdx) continue;
            if (_config.MaxAdx > 0 && adx > _config.MaxAdx) continue;

            // (e) RVOL — stock must be "in play"
            var rvol = ComputeRvol(byDay, priorDays, todayCandles, i - firstTodayIdx);
            if (_config.MinRvol > 0 && rvol < _config.MinRvol) continue;

            // (f) trigger volume expansion
            if (_config.MinTriggerVolMult > 0)
            {
                var volMult = ComputeTriggerVolMult(sortedIntraday, i, 20);
                if (volMult < _config.MinTriggerVolMult) continue;
            }

            result = new ScanResult(c, atr, rvol, adx, gapPct);
            return true;
        }

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
