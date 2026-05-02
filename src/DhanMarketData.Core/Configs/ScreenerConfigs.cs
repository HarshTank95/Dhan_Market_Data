namespace DhanMarketData.Configs;

/// <summary>
/// Base configuration for all screeners
/// </summary>
public class ScreenerConfig
{
    public int ScreeningCandleCount { get; set; } = 3;
}

/// <summary>
/// Volume Spike Screener configuration
/// Detects high volume green candles at market open
/// </summary>
public class VolumeSpikeConfig : ScreenerConfig
{
    public decimal VolumeMultiplier { get; set; } = 3.0m;
    public decimal CandleSizeMultiplier { get; set; } = 2.0m;
}

/// <summary>
/// Breakout Screener configuration
/// Detects price breakouts from consolidation zones
/// </summary>
public class BreakoutConfig : ScreenerConfig
{
    public decimal VolumeMultiplier { get; set; } = 2.0m;
    public decimal BreakoutThreshold { get; set; } = 0.95m;
    public int LookbackPeriod { get; set; } = 10;
}

/// <summary>
/// Dominance Candle Screener configuration
/// Identifies strong bullish candles with dominant body
/// </summary>
public class DominanceCandleConfig
{
    public decimal MinBodyPercent { get; set; } = 70m;
    public decimal MaxBodyPercent { get; set; } = 80m;
    public decimal MinWickPercent { get; set; } = 5m;
    public decimal MinCandleSizeMultiplier { get; set; } = 1.0m;
    public decimal MaxCandleSizeMultiplier { get; set; } = 2.0m;
    public decimal VolumeMultiplier { get; set; } = 1.5m;
    public double MinAbsoluteVolume { get; set; } = 2000;
    public decimal MaxMovementMultiplier { get; set; } = 2.0m;
    public decimal MaxGapUpPercent { get; set; } = 2.0m;   // Skip if gap-up > this % (bullish gap)
    public decimal MaxGapDownPercent { get; set; } = 1.0m; // Skip if gap-down > this % (bearish gap)
    public int HistoricalDays { get; set; } = 10;
    public TimeSpan EntryBracketStart { get; set; } = new TimeSpan(9, 15, 0);
    public TimeSpan EntryBracketEnd { get; set; } = new TimeSpan(9, 45, 0);
}

/// <summary>
/// Opening Range Breakout Screener configuration
/// Identifies stocks with clean gap-up structure and breakout above opening range
/// Best used with 5-min timeframe for precise opening range calculation
/// </summary>
public class OpeningRangeConfig : ScreenerConfig
{
    /// <summary>
    /// Minimum gap percentage above previous day close (default: 0.5%)
    /// </summary>
    public decimal MinGapPercent { get; set; } = 0.5m;

    /// <summary>
    /// Maximum gap percentage above previous day close (default: 2.0%)
    /// Filters out excessive gap-ups (more risk of reversal)
    /// </summary>
    public decimal MaxGapPercent { get; set; } = 2.0m;

    /// <summary>
    /// Maximum upper wick percentage allowed in clean candles (default: 30%)
    /// Upper wick ratio = (High - Close) / (High - Low) * 100
    /// </summary>
    public decimal MaxUpperWickPercent { get; set; } = 30m;

    /// <summary>
    /// Minimum volume multiplier vs historical average (default: 1.5x)
    /// </summary>
    public decimal MinVolumeMultiplier { get; set; } = 1.5m;

    /// <summary>
    /// Number of candles to check for clean structure (default: 2)
    /// Typically first two 5-min candles after market open
    /// </summary>
    public int CleanCandleCount { get; set; } = 2;

    /// <summary>
    /// Opening range duration in minutes (default: 10 minutes = 9:15 to 9:25)
    /// For 5min timeframe: 10 min = 2 candles
    /// </summary>
    public int OpeningRangeMinutes { get; set; } = 10;

    /// <summary>
    /// End of observation phase - no trades before this time (default: 9:25)
    /// System observes price action during this period
    /// </summary>
    public TimeSpan ObservationEndTime { get; set; } = new TimeSpan(9, 25, 0);

    /// <summary>
    /// Execution window start - earliest time to enter trade (default: 9:25)
    /// </summary>
    public TimeSpan ExecutionWindowStart { get; set; } = new TimeSpan(9, 25, 0);

    /// <summary>
    /// Execution window end - latest time to enter trade (default: 9:45)
    /// Breakout must occur within this window
    /// </summary>
    public TimeSpan ExecutionWindowEnd { get; set; } = new TimeSpan(9, 45, 0);

    /// <summary>
    /// Number of historical days to calculate averages (default: 10)
    /// Used for volume and candle size comparisons
    /// </summary>
    public int HistoricalDaysForAverage { get; set; } = 10;
    /// <summary>
    /// Maximum allowed multiple of average candle size for opening-range candles (default: 3x)
    /// If any opening-range candle size > AvgSize * MaxCandleSizeMultiplier, screener rejects the stock.
    /// </summary>
    public decimal MaxCandleSizeMultiplier { get; set; } = 3.0m;
}
