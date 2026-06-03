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
        new StrategyPreset
        {
            Id = 5,
            Name = "Gap Fade (Long)",
            Description = "Quiet, ATR-normalized gap-downs on liquid trending stocks; confirmation-candle entry inside 09:30–10:15 window; mean-reversion long.",
            IsBuiltIn = true,
            ScreenerType = "gapfade",
            StrategyType = "gapfadelong",
            ScreenerConfigJson = """
                {
                  "MinGapAtrRatio": 0.20,
                  "MaxGapAtrRatio": 0.60,
                  "RequirePartialGap": true,
                  "RequireUnfilledAtEntry": true,
                  "MaxOpeningVolumeMultiplier": 0.8,
                  "MinAbsoluteVolume": 1000,
                  "VolumeAverageDays": 10,
                  "MinAverageDailyVolume": 500000,
                  "MinPrice": 100,
                  "RequireUptrend": true,
                  "SmaPeriod": 20,
                  "AtrPeriod": 14,
                  "MinHistoricalDays": 25,
                  "MaxStopLossPercent": 1.0
                }
                """,
            StrategyConfigJson = """
                {
                  "EntryWindowStart": "09:30:00",
                  "EntryWindowEnd": "10:15:00",
                  "RequireConfirmationCandle": true,
                  "TimeExit": "12:30:00"
                }
                """,
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 6,
            Name = "Volume Confluence Breakout (Long)",
            Description = "15-min ORB on F&O-eligible NSE stocks, filtered by cash RVOL and confirmed by futures OI direction. Long buildup = full size; short covering = half size. v1 uses 5-min timeframe (15-min OI delta requires multi-candle OR), MinScoreThreshold instead of cross-stock top-N ranking, and includes spec §12 cost model (0.10% RT).",
            IsBuiltIn = true,
            ScreenerType = "rvolorb",
            StrategyType = "confluenceorblong",
            ScreenerConfigJson = """
                {
                  "OpeningRangeMinutes": 15,
                  "DojiThreshold": 0.10,
                  "RvolLookbackDays": 14,
                  "MinRvol": 1.0,
                  "MinScoreThreshold": 1.0,
                  "RequireFnoOnly": true,
                  "MinPrice": 50,
                  "MinAvgRupeeVolume": 1000000000,
                  "MinAtrPercent": 1.0,
                  "MaxYesterdayRangePct": 9.0,
                  "AtrLookback": 14,
                  "RequireOiConfluence": false,
                  "MinOiDeltaPercent": 1.0,
                  "SkipDayIfIndiaVixAbove": 25.0,
                  "SkipDayIfNiftyGapPct": 2.0,
                  "MinHistoricalDays": 28,
                  "SkipTuesday": true,
                  "MinGapPct": -1.5
                }
                """,
            StrategyConfigJson = """
                {
                  "AtrStopMultiplier": 0.30,
                  "NoFillCutoff": "13:00:00",
                  "EntryNotBefore": "10:00:00",
                  "EntryNotAfter": "10:30:00",
                  "MinBreakoutVolMult": 0.5,
                  "ExitTime": "14:30:00",
                  "DayMultiplierMon": 1.0,
                  "DayMultiplierTue": 1.0,
                  "DayMultiplierWed": 1.0,
                  "DayMultiplierThu": 1.0,
                  "DayMultiplierFri": 1.0,
                  "CostModelRoundTripPct": 0.10
                }
                """,
            // Custom TradingConfig (NOT the shared one) — RVOL+ORB+OI is a
            // multi-position portfolio strategy with different sizing math:
            //   - MaxTradesPerDay 10 (vs shared 2) — strategy aims for up to
            //     10 concurrent positions per spec §5.3 / §6.3.
            //   - MaxCapitalPerTrade 100000 = ₹1L per slice (₹10L total / 10).
            //   - FixedStopLoss 1000 = 1% × slice = base_risk per spec §6.3.
            //   - FixedTarget 0 — there is NO PROFIT TARGET (spec §6.4).
            //   - EntryTime / ExitTime here are scaffolding; the strategy
            //     uses its own ConfluenceOrbStrategyConfig.ExitTime (14:30).
            TradingConfigJson = """
                {
                  "MarketOpenTime": "09:15:00",
                  "MarketCloseTime": "15:30:00",
                  "EntryTime": "09:30:00",
                  "ExitTime": "14:30:00",
                  "TargetMultiplier": 1.0,
                  "FixedStopLoss": 1000,
                  "FixedTarget": 0,
                  "RequireCloseAboveDayOpen": false,
                  "TrailStepMultiplier": 1.0,
                  "MaxTradesPerDay": 10,
                  "MaxCapitalPerTrade": 100000
                }
                """,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 7,
            Name = "EMA Gap-Down Reclaim (Long)",
            Description = "Buy-the-dip: uptrending stocks (2–10% above their 20-day SMA) that gapped DOWN ≥1.5%, entered when price reclaims the 9-EMA intraday (pullback touch + bullish close), filtered to moderate trend strength (ADX ≤ 25 — high ADX = exhausted move). SL at trigger low, 1.5R target, hard exit 15:00 IST. The gap-down-in-uptrend is the stock selection; the 9-EMA reclaim is the trigger. Tuned on 500 NSE stocks × 250 days (net +52k, PF ~4.5, 77% win, max drawdown 3% — IN-SAMPLE, paper-test before live; the ADX≤25 cut is data-selected and will regress somewhat).",
            IsBuiltIn = true,
            ScreenerType = "emapullback",
            StrategyType = "emapullback",
            ScreenerConfigJson = """
                {
                  "FastEmaPeriod": 9,
                  "SlowEmaPeriod": 20,
                  "SlopeLookback": 5,
                  "MinEmaDistanceAtr": 0.3,
                  "MaxEmaDistanceAtr": 1.5,
                  "MinDailyAtrPct": 1.5,
                  "DailyAtrPeriod": 14,
                  "MinDailyTrendPct": 2.0,
                  "MaxDailyTrendPct": 10.0,
                  "DailyTrendSmaPeriod": 20,
                  "IntradayAtrPeriod": 14,
                  "MorningStart": "10:00:00",
                  "MorningEnd": "11:00:00",
                  "AfternoonStart": "13:30:00",
                  "AfternoonEnd": "14:00:00",
                  "RequireEngulfing": true,
                  "MinStopDistancePct": 0.45,
                  "MaxStopDistancePct": 1.5,
                  "MinRvol": 0,
                  "RvolLookbackDays": 10,
                  "MinAdx": 0,
                  "MaxAdx": 25,
                  "AdxPeriod": 14,
                  "MinTriggerVolMult": 0,
                  "MinGapPct": 0,
                  "MaxGapPct": 5,
                  "MaxEntryGapPct": -1.5,
                  "MinPrice": 100,
                  "MinAverageDailyVolume": 500000,
                  "MinHistoricalDays": 25
                }
                """,
            StrategyConfigJson = """
                {
                  "RiskRewardRatio": 1.5,
                  "HardExitTime": "15:00:00",
                  "UseTrailingStop": false,
                  "TrailActivateR": 1.0,
                  "TrailGapR": 1.0,
                  "TrailHardTargetR": 0,
                  "CostModelRoundTripPct": 0.10
                }
                """,
            TradingConfigJson = SharedTradingConfigJson,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
        new StrategyPreset
        {
            Id = 8,
            Name = "VWAP ORB Momentum (Long)",
            Description = "Momentum: on a trending Mon/Wed session, a liquid (≥30L/day prior avg), higher-priced (≥₹500) stock that gapped ≥0% breaks above its 30-min opening-range high while holding above a RISING session VWAP (slope 20–50 bps — with-flow but not exhausted) and the opening range was wide (≥1%). Enter next bar's open; SL = min(VWAP, breakout-bar low); HELD TO 15:00 IST (only the stop exits earlier). The day + liquidity + price + OR-width + slope-band + gap selection is the edge; the opening-range break is the trigger. Developed via the offline diagnostic harness on 414 NSE stocks × ~250 days (5-min), fully non-lookahead: raw signal was gross-negative; this stacked config reached ~88 trades, +0.93R/trade (~₹466 at ₹500 risk), 63% win, 11/13 months positive. Mon/Wed avoids Tue/Thu/Fri expiry-day chop (corroborated across two VWAP strategies). IN-SAMPLE — paper-test before live.",
            IsBuiltIn = true,
            ScreenerType = "vwaporb",
            StrategyType = "vwaporb",
            ScreenerConfigJson = """
                {
                  "OpeningRangeBars": 6,
                  "MinOrWidthPct": 1.0,
                  "VwapSlopeLookback": 3,
                  "MinVwapSlopeBps": 20,
                  "MaxVwapSlopeBps": 50,
                  "MinGapPct": 0,
                  "WindowStart": "09:45:00",
                  "WindowEnd": "14:00:00",
                  "AllowMon": true,
                  "AllowTue": false,
                  "AllowWed": true,
                  "AllowThu": false,
                  "AllowFri": false,
                  "MinStopDistancePct": 0.5,
                  "MaxStopDistancePct": 0,
                  "MinPrice": 500,
                  "MinAverageDailyVolume": 3000000,
                  "VolumeLookbackDays": 20,
                  "MinHistoricalDays": 20
                }
                """,
            StrategyConfigJson = """
                {
                  "HardExitTime": "15:00:00",
                  "ExitOnCloseBelowVwap": false,
                  "HardTargetR": 0,
                  "RiskPerTrade": 500,
                  "CostModelRoundTripPct": 0.10
                }
                """,
            // Custom TradingConfig (deviates from SharedTradingConfigJson): VWAP ORB
            // takes every qualifying breakout across the Mon/Wed universe (well under
            // the cap on most days, but heavy days can exceed 2). The shared
            // MaxTradesPerDay=2 cap would starve it; raised to 20. Same pattern as
            // the Volume Confluence preset.
            TradingConfigJson = """
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
                  "MaxTradesPerDay": 20,
                  "MaxCapitalPerTrade": 300000
                }
                """,
            CreatedAt = SeedTimestamp,
            UpdatedAt = SeedTimestamp,
        },
    };
}
