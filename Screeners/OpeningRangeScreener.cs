using DhanMarketData.Core.Interfaces;
using DhanMarketData.Core.Models;
using DhanMarketData.Configs;

namespace DhanMarketData.Screeners;

/// <summary>
/// Opening Range Breakout Screener: Identifies stocks with clean gap-up structure
/// and validates opening range for potential breakout trades.
/// 
/// Criteria:
/// - Gap ≥ 1.2% above previous day close
/// - First N candles have clean structure (≤ 30% upper wick)
/// - Strong volume expansion (≥ 1.5x average)
/// - Bullish structure (close > previous close)
/// - Calculates opening range from first N minutes (default: 10 min = 9:15-9:25)
/// 
/// Best used with 5-min timeframe for precise opening range calculation.
/// </summary>
public class OpeningRangeScreener : IScreener
{
    private readonly OpeningRangeConfig _config;
    private static readonly TimeSpan IstOffset = TimeSpan.FromHours(5.5);

    public string Name => "Opening Range Breakout Screener";
    public string Description => "Identifies clean gap-up stocks with opening range breakout potential";

    public OpeningRangeScreener(OpeningRangeConfig? config = null)
    {
        _config = config ?? new OpeningRangeConfig();
    }

    public bool MeetsConditions(List<Candle> allCandles, out List<Candle> signalCandles)
    {
        signalCandles = new List<Candle>();

        if (allCandles == null || allCandles.Count == 0)
            return false;

        // Split into historical and current day candles
        var currentDayStart = allCandles.Last().Timestamp.Date;
        var historicalCandles = allCandles.Where(c => c.Timestamp.Date < currentDayStart).ToList();
        var currentDayCandles = allCandles.Where(c => c.Timestamp.Date == currentDayStart).ToList();

        if (currentDayCandles.Count < _config.CleanCandleCount)
            return false;

        // 1. Get previous day's close price
        var previousDayClose = GetPreviousDayClose(historicalCandles);
        if (previousDayClose == 0)
            return false; // No historical data

        // 2. Check gap condition (within configured range: min-max)
        var firstCandle = currentDayCandles.First();
        var gapPercent = ((firstCandle.Open - previousDayClose) / previousDayClose) * 100;
        
        // Gap must be within min-max range
        if (gapPercent < _config.MinGapPercent || gapPercent > _config.MaxGapPercent)
            return false;

        // 3. Check clean candle structure for first N candles
        var candlesToCheck = currentDayCandles.Take(_config.CleanCandleCount).ToList();
        if (!HasCleanCandleStructure(candlesToCheck))
            return false;

        // 4. Calculate historical averages for volume validation
        var avgVolume = CalculateAverageVolume(historicalCandles);
        if (avgVolume == 0)
            return false;

        // 5. Check volume expansion (≥ 1.5x average)
        foreach (var candle in candlesToCheck)
        {
            if (candle.Volume < avgVolume * (long)_config.MinVolumeMultiplier)
                return false;
        }

        // 6. Validate bullish structure (current close > previous close)
        var lastCandle = currentDayCandles.Last();
        if (lastCandle.Close <= previousDayClose)
            return false;

        // 7. Calculate opening range from first N minutes
        var openingRangeCandles = GetOpeningRangeCandles(currentDayCandles);
        if (openingRangeCandles.Count == 0)
            return false;

        // 8. Check opening-range candle size vs historical average (avoid unusually large candles)
        var avgCandleSize = CalculateAverageCandleSize(historicalCandles);
        if (avgCandleSize == 0)
            return false; // cannot validate without history

        // Reject if any opening range candle size > configured multiple of average
        foreach (var c in openingRangeCandles)
        {
            var size = c.High - c.Low;
            if (size > avgCandleSize * _config.MaxCandleSizeMultiplier)
                return false;
        }

        // Return opening range candles as signal candles for strategy
        signalCandles = openingRangeCandles;
        return true;
    }

    /// <summary>
    /// Gets previous trading day's closing price from historical candles.
    /// Handles weekends and holidays by finding the last available candle.
    /// </summary>
    private decimal GetPreviousDayClose(List<Candle> historicalCandles)
    {
        if (historicalCandles.Count == 0)
            return 0;

        // Get last candle from historical data (last candle of previous trading day)
        var lastHistoricalCandle = historicalCandles
            .OrderBy(c => c.Timestamp)
            .LastOrDefault();

        return lastHistoricalCandle?.Close ?? 0;
    }

    /// <summary>
    /// Checks if candles have clean structure with minimal upper wicks (≤ 30%).
    /// Upper wick ratio = (High - Close) / (High - Low) * 100
    /// </summary>
    private bool HasCleanCandleStructure(List<Candle> candles)
    {
        foreach (var candle in candles)
        {
            var range = candle.High - candle.Low;
            if (range == 0)
                continue; // Skip doji/flat candles

            var upperWick = candle.High - candle.Close;
            var upperWickPercent = (upperWick / range) * 100;

            if (upperWickPercent > _config.MaxUpperWickPercent)
                return false; // Rejection at highs, not clean
        }

        return true;
    }

    /// <summary>
    /// Calculates average volume from historical candles (last N days).
    /// </summary>
    private long CalculateAverageVolume(List<Candle> historicalCandles)
    {
        if (historicalCandles.Count == 0)
            return 0;

        // Take last N days for average
        var recentCandles = historicalCandles
            .OrderByDescending(c => c.Timestamp)
            .Take(_config.HistoricalDaysForAverage * 75) // ~75 candles per day for 5min
            .ToList();

        if (recentCandles.Count == 0)
            return 0;

        return (long)recentCandles.Average(c => c.Volume);
    }

    /// <summary>
    /// Calculates average candle size (High - Low) from historical candles
    /// Uses last N days as defined in config (approx N * 75 candles per day for 5-min)
    /// </summary>
    private decimal CalculateAverageCandleSize(List<Candle> historicalCandles)
    {
        if (historicalCandles.Count == 0)
            return 0;

        var recentCandles = historicalCandles
            .OrderByDescending(c => c.Timestamp)
            .Take(_config.HistoricalDaysForAverage * 75)
            .ToList();

        if (recentCandles.Count == 0)
            return 0;

        var avgSize = recentCandles.Average(c => (double)(c.High - c.Low));
        return (decimal)avgSize;
    }

    /// <summary>
    /// Gets candles within the opening range period (e.g., 9:15-9:25).
    /// These candles define the high/low range for breakout detection.
    /// </summary>
    private List<Candle> GetOpeningRangeCandles(List<Candle> currentDayCandles)
    {
        var marketOpen = new TimeSpan(9, 15, 0);
        var rangeEnd = _config.ObservationEndTime;

        var marketOpenUtc = IstToUtc(marketOpen);
        var rangeEndUtc = IstToUtc(rangeEnd);

        var rangeCandles = currentDayCandles
            .Where(c =>
            {
                var timeOfDay = c.Timestamp.TimeOfDay;
                return timeOfDay >= marketOpenUtc && timeOfDay < rangeEndUtc;
            })
            .ToList();

        // Validate we have enough candles for opening range
        var expectedCandles = _config.OpeningRangeMinutes / 5; // For 5-min timeframe
        if (rangeCandles.Count < expectedCandles)
            return new List<Candle>(); // Not enough data

        return rangeCandles;
    }

    /// <summary>
    /// Converts IST time to UTC for comparison with candle timestamps.
    /// </summary>
    private TimeSpan IstToUtc(TimeSpan istTime)
    {
        var utcTime = istTime - IstOffset;
        return utcTime < TimeSpan.Zero ? utcTime + TimeSpan.FromDays(1) : utcTime;
    }
}
