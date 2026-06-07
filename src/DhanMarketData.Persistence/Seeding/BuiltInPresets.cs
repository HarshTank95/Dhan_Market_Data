using DhanMarketData.Persistence.Entities;

namespace DhanMarketData.Persistence.Seeding;

// Built-in strategy presets seeded into the EF model via AppDbContext.HasData.
//
// IsBuiltIn = true means: cannot be deleted, but Reset re-applies these values
// and Clone creates a user copy.
//
// Retired 2026-06-07: Gap Fade (Long) [Id 5], Volume Confluence Breakout (Long)
// [Id 6] and EMA Gap-Down Reclaim (Long) [Id 7] were removed. All three consumed
// daily candles and their historical backtests were inflated by the daily-bar
// look-ahead (fixed in DhanDataApiClient.GetDailyHistoricalAsync); re-run on the
// corrected data they had no edge, and Volume Confluence additionally relied on a
// stop-market fill at OR.High that isn't achievable live (~93% of its apparent
// edge). Their screener/strategy/config classes were deleted and the seed rows
// dropped via the RemoveCorruptedPresets migration. VWAP ORB (Id 8) is the sole
// surviving built-in — it never used daily candles and validated clean. See
// docs/strategies.md.
public static class BuiltInPresets
{
    // Deterministic seed timestamp. Using a fixed instant keeps migration
    // diffs reproducible across machines.
    private static readonly DateTime SeedTimestamp =
        new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static IReadOnlyList<StrategyPreset> All() => new[]
    {
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
            // Custom TradingConfig (deviates from a shared default): VWAP ORB takes
            // every qualifying breakout across the Mon/Wed universe (well under the
            // cap on most days, but heavy days can exceed 2). A MaxTradesPerDay=2
            // cap would starve it; raised to 20.
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
