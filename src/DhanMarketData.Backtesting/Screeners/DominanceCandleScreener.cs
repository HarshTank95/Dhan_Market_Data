using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Dominance Candle Screener: Identifies strong bullish candles with dominant body
/// - Body: 70-80% of total range
/// - Wicks: Each ≥5% of total range
/// - Size: Between 1x and 2x of 10-day average
/// - Volume: Above average AND minimum absolute volume
/// - Timing: Within configured entry bracket
/// </summary>
public class DominanceCandleScreener : IScreener
{
    private readonly DominanceCandleConfig _config;
    private static readonly TimeSpan IstOffset = new TimeSpan(5, 30, 0);

    public string Name => "Dominance Candle Screener";
    public string Description => $"Body: {_config.MinBodyPercent}-{_config.MaxBodyPercent}%, Min Wick: {_config.MinWickPercent}%, Size: {_config.MinCandleSizeMultiplier}x-{_config.MaxCandleSizeMultiplier}x avg";

    public DominanceCandleScreener(DominanceCandleConfig config)
    {
        _config = config;
    }

    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }

    public bool MeetsConditions(List<Candle> candles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();

        if (candles.Count < _config.HistoricalDays * 75) // Need ~10 days of 5min data (75 candles/day)
            return false;

        // Split: last 10 days for average, current day for screening
        var historicalCandles = candles.Take(candles.Count - 75).ToList();
        var currentDayCandles = candles.Skip(candles.Count - 75).ToList();

        // Gap filter: Check if stock gapped up/down from previous day's close
        if (historicalCandles.Count > 0 && currentDayCandles.Count > 0)
        {
            var previousDayClose = historicalCandles.Last().Close;
            var todayOpen = currentDayCandles.First().Open;
            var gapPercent = ((todayOpen - previousDayClose) / previousDayClose) * 100;
            
            // Check gap-up (positive gap)
            if (gapPercent > _config.MaxGapUpPercent)
                return false;
            
            // Check gap-down (negative gap)
            if (gapPercent < -_config.MaxGapDownPercent)
                return false;
        }

        // Calculate 10-day average candle size and volume
        var avgCandleSize = historicalCandles.Average(c => c.High - c.Low);
        if (avgCandleSize <= 0)
            return false;

        var avgVolume = (decimal)historicalCandles.Average(c => c.Volume);
        if (avgVolume <= 0)
            return false;

        // Convert configured IST entry bracket to UTC
        var windowStart = IstToUtc(_config.EntryBracketStart);
        var windowEnd = IstToUtc(_config.EntryBracketEnd);

        // Get day's open price for movement calculation
        var dayOpen = currentDayCandles.Count > 0 ? currentDayCandles[0].Open : 0m;

        // Find dominance candles in the configured time bracket
        for (int candleIndex = 0; candleIndex < currentDayCandles.Count; candleIndex++)
        {
            var candle = currentDayCandles[candleIndex];
            var timeOfDay = candle.Timestamp.TimeOfDay;
            
            // Must be within configured entry bracket
            if (timeOfDay < windowStart || timeOfDay > windowEnd)
                continue;

            var range = candle.High - candle.Low;
            if (range <= 0)
                continue;

            // Must be bullish
            var body = candle.Close - candle.Open;
            if (body <= 0)
                continue;

            // Check body percentage (70-80%)
            var bodyPercent = (body / range) * 100;
            if (bodyPercent < _config.MinBodyPercent || bodyPercent > _config.MaxBodyPercent)
                continue;

            // Check wick sizes (both must be ≥5%)
            var upperWick = candle.High - candle.Close;
            var lowerWick = candle.Open - candle.Low;
            var upperWickPercent = (upperWick / range) * 100;
            var lowerWickPercent = (lowerWick / range) * 100;

            // Both wicks must meet minimum requirement (AND condition)
            if (!(upperWickPercent >= _config.MinWickPercent && lowerWickPercent >= _config.MinWickPercent))
                continue;

            // Check candle size (between 1x and 2x average)
            var sizeMultiplier = range / avgCandleSize;
            if (sizeMultiplier < _config.MinCandleSizeMultiplier || sizeMultiplier > _config.MaxCandleSizeMultiplier)
                continue;

            // Check volume (must be > 1.5x average volume)
            if (candle.Volume < avgVolume * _config.VolumeMultiplier)
                continue;

            // Check minimum absolute volume (must be >= 2000)
            if (candle.Volume < _config.MinAbsoluteVolume)
                continue;

            // Check if all candles from market open till this dominance candle have >= MinAbsoluteVolume
            bool allCandlesHaveVolume = true;
            for (int j = 0; j <= candleIndex; j++)
            {
                if (currentDayCandles[j].Volume < _config.MinAbsoluteVolume)
                {
                    allCandlesHaveVolume = false;
                    break;
                }
            }
            if (!allCandlesHaveVolume)
                continue;

            // Check if stock has moved excessively from day's open
            var numberOfCandles = candleIndex + 1;
            var expectedMovement = numberOfCandles * avgCandleSize;
            var actualMovement = Math.Abs(candle.Close - dayOpen);
            
            if (actualMovement > _config.MaxMovementMultiplier * expectedMovement)
                continue;

            // Found dominance candle!
            signalCandles.Add(candle);
        }

        return signalCandles.Count > 0;
    }
}
