using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Gap Fade (Long) Screener: identifies quiet gap-down setups suitable for
/// mean-reversion (gap-fill) entries.
///
/// Research-backed filter chain (early-out cheap → expensive):
///  1. Min daily history available (ATR + SMA need it)
///  2. Min price floor (skip penny stocks)
///  3. Gap direction (gap-down)
///  4. ATR-normalized gap size in [MinGapAtrRatio, MaxGapAtrRatio]
///  5. Partial gap (open above prior day low)
///  6. Gap not already filled in 9:15 candle
///  7. Trend filter (prev close &gt; SMA(N))
///  8. Liquidity (avg daily volume ≥ floor)
///  9. Quiet-volume catalyst filter (9:15 vol ≤ historical 9:15-bar avg × mult)
/// 10. Min absolute volume sanity floor
/// 11. SL not insanely wide (gap candle low not too far below open)
///
/// Returns the 9:15 candle as the signalCandles list — the strategy uses
/// firstCandle.Low as its SL anchor.
/// </summary>
public class GapFadeScreener : IScreener
{
    private readonly GapFadeConfig _config;

    public string Name => "Gap Fade (Long) Screener";
    public string Description => "Quiet, ATR-normalized gap-downs on liquid trending stocks (mean-reversion candidates)";

    public int RequiredHistoricalDays => _config.MinHistoricalDays;
    public bool RequiresDailyCandles => true;

    public GapFadeScreener(GapFadeConfig? config = null)
    {
        _config = config ?? new GapFadeConfig();
    }

    // Intraday-only fallback: this screener requires daily candles, so the
    // legacy single-list path returns false. Orchestrator must use the
    // ScreenerContext overload (which it does when RequiresDailyCandles=true).
    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();
        return false;
    }

    public bool MeetsConditions(ScreenerContext context, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();

        var intraday = context.Intraday;
        var daily = context.Daily;

        if (intraday == null || intraday.Count == 0) return false;
        if (daily == null || daily.Count < _config.MinHistoricalDays) return false;

        // Split intraday into current day vs historical
        var currentDay = intraday.Last().Timestamp.Date;
        var currentIntraday = intraday.Where(c => c.Timestamp.Date == currentDay).ToList();
        if (currentIntraday.Count == 0) return false;

        var firstCandle = currentIntraday.First();

        // 1. Min price (skip penny stocks)
        if (firstCandle.Open < _config.MinPrice) return false;

        // 2. Prior day's daily candle (most recent in daily list — already filtered to < currentDay by orchestrator)
        var sortedDaily = daily.OrderBy(c => c.Timestamp).ToList();
        var prevDay = sortedDaily[^1];
        var previousDayClose = prevDay.Close;
        var previousDayLow = prevDay.Low;
        if (previousDayClose <= 0) return false;

        // 3. Gap direction: gap-down only
        if (firstCandle.Open >= previousDayClose) return false;
        var gapSize = previousDayClose - firstCandle.Open;

        // 4. ATR-normalized gap size (the dominant predictor)
        var atr = ComputeAtrWilder(sortedDaily, _config.AtrPeriod);
        if (atr <= 0) return false;
        var gapAtrRatio = gapSize / atr;
        if (gapAtrRatio < _config.MinGapAtrRatio || gapAtrRatio > _config.MaxGapAtrRatio) return false;

        // 5. Partial gap (open above prior day low)
        if (_config.RequirePartialGap && firstCandle.Open <= previousDayLow) return false;

        // 6. Gap not already filled in the first 5 min
        if (_config.RequireUnfilledAtEntry && firstCandle.High >= previousDayClose) return false;

        // 7. Trend filter (prev close > SMA)
        if (_config.RequireUptrend)
        {
            var sma = ComputeSma(sortedDaily, _config.SmaPeriod);
            if (sma <= 0) return false;
            if (previousDayClose <= sma) return false;
        }

        // 8. Liquidity: 20-day avg daily volume floor
        var dailyAvgVolDays = Math.Min(20, sortedDaily.Count);
        var avgDailyVol = sortedDaily
            .OrderByDescending(c => c.Timestamp)
            .Take(dailyAvgVolDays)
            .Average(c => (double)c.Volume);
        if (avgDailyVol < _config.MinAverageDailyVolume) return false;

        // 9. Quiet-volume catalyst filter (9:15 candle vol ≤ historical 9:15-bar avg × mult)
        var avg9_15 = ComputeAvg9_15BarVolume(intraday, currentDay, _config.VolumeAverageDays);
        if (avg9_15 <= 0) return false;
        if ((double)firstCandle.Volume > avg9_15 * (double)_config.MaxOpeningVolumeMultiplier) return false;

        // 10. Min absolute volume sanity floor
        if (firstCandle.Volume < _config.MinAbsoluteVolume) return false;

        // 11. SL distance check (gap candle low not too far below entry-candidate open)
        var slDistance = firstCandle.Open - firstCandle.Low;
        if (slDistance <= 0) return false;
        var slPercent = (slDistance / firstCandle.Open) * 100m;
        if (slPercent > _config.MaxStopLossPercent) return false;

        // All filters passed — return the 9:15 candle as the SL anchor for the strategy
        signalCandles = new List<Candle> { firstCandle };
        return true;
    }

    /// <summary>
    /// Wilder's ATR (RMA smoothing). Returns the most recent ATR value.
    /// Needs at least `period + 1` daily candles (period TRs require period+1 closes).
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

        // Initial ATR = simple average of first `period` TRs
        decimal atr = trs.Take(period).Average();
        // Wilder smoothing for remaining TRs
        for (int i = period; i < trs.Count; i++)
        {
            atr = ((atr * (period - 1)) + trs[i]) / period;
        }
        return atr;
    }

    /// <summary>Simple moving average of the most recent `period` daily closes.</summary>
    private static decimal ComputeSma(List<Candle> sortedDaily, int period)
    {
        if (sortedDaily == null || sortedDaily.Count < period) return 0;
        return sortedDaily
            .OrderByDescending(c => c.Timestamp)
            .Take(period)
            .Average(c => c.Close);
    }

    /// <summary>
    /// Average volume of the FIRST candle (9:15 IST bar) across the last N
    /// prior trading days. Used by the catalyst filter.
    /// </summary>
    private static double ComputeAvg9_15BarVolume(List<Candle> intraday, DateTime currentDay, int days)
    {
        var firstCandlesByDay = intraday
            .Where(c => c.Timestamp.Date < currentDay)
            .GroupBy(c => c.Timestamp.Date)
            .Select(g => g.OrderBy(c => c.Timestamp).First())
            .OrderByDescending(c => c.Timestamp)
            .Take(days)
            .ToList();

        if (firstCandlesByDay.Count == 0) return 0;
        return firstCandlesByDay.Average(c => (double)c.Volume);
    }
}
