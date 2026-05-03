using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Seeding;

// Built-in strategy presets seeded by the initial migration.
// Values are byte-for-byte from the appsettings.json that was active at the
// time of the multi-project restructure — these are the actively-tuned defaults,
// NOT the C# class field defaults (which had drifted).
//
// IsBuiltIn = true means: cannot be deleted, but Reset re-applies these values
// and Clone creates a user copy.
public static class BuiltInPresets
{
    // Deterministic seed timestamp. Using a fixed instant keeps migration
    // diffs reproducible across machines.
    private static readonly DateTime SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // Shared trading config — every built-in preset starts with these values.
    // Stored verbatim per preset so users can override without affecting siblings.
    private const string SharedTradingConfigJson = """
        {
          "MarketOpenTime": "09:15:00",
          "MarketCloseTime": "15:30:00",
          "EntryTime": "09:30:00",
          "ExitTime": "15:15:00",
          "TargetMultiplier": 2.5,
          "FixedStopLoss": 500,
          "FixedTarget": 2000,
          "RequireCloseAboveDayOpen": false,
          "TrailStepMultiplier": 2.0,
          "MaxTradesPerDay": 2,
          "MaxCapitalPerTrade": 300000
        }
        """;

    public static IReadOnlyList<StrategyPreset> All() => new[]
    {
        new StrategyPreset
        {
            Id = 1,
            Name = "Volume Spike",
            Description = "Early-morning unusual volume; enter at 9:30 open with fixed SL/target.",
            IsBuiltIn = true,
            ScreenerType = "volumespike",
            StrategyType = "fixedtarget",
            ScreenerConfigJson = """
                {
                  "ScreeningCandleCount": 3,
                  "VolumeMultiplier": 2.0,
                  "CandleSizeMultiplier": 3.0
                }
                """,
            StrategyConfigJson = "{}",
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 2,
            Name = "Dominance Breakout",
            Description = "Identify dominance candle in 9:30–10:00 window; enter on next-candle breakout above its high; fixed SL/target.",
            IsBuiltIn = true,
            ScreenerType = "dominancecandle",
            StrategyType = "breakoutentry",
            ScreenerConfigJson = """
                {
                  "MinBodyPercent": 70,
                  "MaxBodyPercent": 85,
                  "MinWickPercent": 5,
                  "MinCandleSizeMultiplier": 1.0,
                  "MaxCandleSizeMultiplier": 2.5,
                  "VolumeMultiplier": 2.0,
                  "MinAbsoluteVolume": 5000,
                  "MaxMovementMultiplier": 2.0,
                  "MaxGapUpPercent": 2.5,
                  "MaxGapDownPercent": 1.0,
                  "HistoricalDays": 10,
                  "EntryBracketStart": "09:30:00",
                  "EntryBracketEnd": "10:00:00"
                }
                """,
            StrategyConfigJson = "{}",
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 3,
            Name = "Dominance Trailing",
            Description = "Same dominance-candle entry as Dominance Breakout, but trailing SL instead of fixed target.",
            IsBuiltIn = true,
            ScreenerType = "dominancecandle",
            StrategyType = "trailingstop",
            ScreenerConfigJson = """
                {
                  "MinBodyPercent": 70,
                  "MaxBodyPercent": 85,
                  "MinWickPercent": 5,
                  "MinCandleSizeMultiplier": 1.0,
                  "MaxCandleSizeMultiplier": 2.5,
                  "VolumeMultiplier": 2.0,
                  "MinAbsoluteVolume": 5000,
                  "MaxMovementMultiplier": 2.0,
                  "MaxGapUpPercent": 2.5,
                  "MaxGapDownPercent": 1.0,
                  "HistoricalDays": 10,
                  "EntryBracketStart": "09:30:00",
                  "EntryBracketEnd": "10:00:00"
                }
                """,
            StrategyConfigJson = "{}",
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 4,
            Name = "Opening Range Breakout",
            Description = "Clean gap-up + opening-range structure; enter on break above OR.High inside the execution window.",
            IsBuiltIn = true,
            ScreenerType = "openingrange",
            StrategyType = "openingrange",
            ScreenerConfigJson = """
                {
                  "MinGapPercent": 0.8,
                  "MaxGapPercent": 10.0,
                  "MaxUpperWickPercent": 80,
                  "MinVolumeMultiplier": 1.5,
                  "CleanCandleCount": 2,
                  "OpeningRangeMinutes": 10,
                  "ObservationEndTime": "09:25:00",
                  "ExecutionWindowStart": "09:40:00",
                  "ExecutionWindowEnd": "09:40:00",
                  "HistoricalDaysForAverage": 10,
                  "MaxCandleSizeMultiplier": 3.0
                }
                """,
            StrategyConfigJson = "{}",
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    };
}
