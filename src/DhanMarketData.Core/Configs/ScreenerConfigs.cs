using DhanMarketData.Configs.Attributes;

namespace DhanMarketData.Configs;

/// <summary>
/// Base configuration for all screeners
/// </summary>
public class ScreenerConfig
{
    [ConfigField(Label = "Screening Candle Count",
                 Description = "Number of candles to evaluate at the start of the session",
                 Group = "General", Kind = ConfigFieldKind.Integer, Min = 1, Max = 20, Order = 0)]
    public int ScreeningCandleCount { get; set; } = 3;
}

/// <summary>
/// Breakout Screener configuration
/// Detects price breakouts from consolidation zones
/// </summary>
public class BreakoutConfig : ScreenerConfig
{
    [ConfigField(Label = "Volume Multiplier",
                 Description = "Minimum volume relative to historical average",
                 Group = "Volume", Kind = ConfigFieldKind.Multiplier, Min = 1.0, Step = 0.1, Unit = "x", Order = 0)]
    public decimal VolumeMultiplier { get; set; } = 2.0m;

    [ConfigField(Label = "Breakout Threshold",
                 Description = "Close must be at least this fraction of the way through the lookback range",
                 Group = "Breakout", Kind = ConfigFieldKind.Number, Min = 0, Max = 1, Step = 0.01, Order = 0)]
    public decimal BreakoutThreshold { get; set; } = 0.95m;

    [ConfigField(Label = "Lookback Period",
                 Description = "Number of candles to compute the consolidation range over",
                 Group = "Breakout", Kind = ConfigFieldKind.Integer, Min = 1, Max = 100, Order = 1)]
    public int LookbackPeriod { get; set; } = 10;
}
