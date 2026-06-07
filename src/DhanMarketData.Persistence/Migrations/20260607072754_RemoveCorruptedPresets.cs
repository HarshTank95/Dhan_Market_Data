using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCorruptedPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Gap Fade (5), Volume Confluence Breakout (6) and EMA Gap-Down
            // Reclaim (7) are removed entirely — they consumed daily candles and
            // their backtests were inflated by the daily-bar look-ahead (now
            // fixed); re-run clean they had no edge, and Volume Confluence also
            // relied on an unachievable OR.High fill. The BacktestRuns →
            // StrategyPresets FK is Restrict, so dependent run history must be
            // deleted first or the DeleteData below fails. Trades go first
            // (TradeRecords → BacktestRuns), then the runs, then the presets.
            // User-confirmed: delete this run history.
            migrationBuilder.Sql(
                "DELETE FROM TradeRecords WHERE BacktestRunId IN " +
                "(SELECT Id FROM BacktestRuns WHERE StrategyPresetId IN (5, 6, 7));");
            migrationBuilder.Sql(
                "DELETE FROM BacktestRuns WHERE StrategyPresetId IN (5, 6, 7);");

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 7);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[,]
                {
                    { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quiet, ATR-normalized gap-downs on liquid trending stocks; confirmation-candle entry inside 09:30–10:15 window; mean-reversion long.", true, "Gap Fade (Long)", "{\n  \"MinGapAtrRatio\": 0.20,\n  \"MaxGapAtrRatio\": 0.60,\n  \"RequirePartialGap\": true,\n  \"RequireUnfilledAtEntry\": true,\n  \"MaxOpeningVolumeMultiplier\": 0.8,\n  \"MinAbsoluteVolume\": 1000,\n  \"VolumeAverageDays\": 10,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinPrice\": 100,\n  \"RequireUptrend\": true,\n  \"SmaPeriod\": 20,\n  \"AtrPeriod\": 14,\n  \"MinHistoricalDays\": 25,\n  \"MaxStopLossPercent\": 1.0\n}", "gapfade", "{\n  \"EntryWindowStart\": \"09:30:00\",\n  \"EntryWindowEnd\": \"10:15:00\",\n  \"RequireConfirmationCandle\": true,\n  \"TimeExit\": \"12:30:00\"\n}", "gapfadelong", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "15-min ORB on F&O-eligible NSE stocks, filtered by cash RVOL and confirmed by futures OI direction. Long buildup = full size; short covering = half size. v1 uses 5-min timeframe (15-min OI delta requires multi-candle OR), MinScoreThreshold instead of cross-stock top-N ranking, and includes spec §12 cost model (0.10% RT).", true, "Volume Confluence Breakout (Long)", "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.0,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": false,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28,\n  \"SkipTuesday\": true,\n  \"MinGapPct\": -1.5\n}", "rvolorb", "{\n  \"AtrStopMultiplier\": 0.30,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"EntryNotBefore\": \"10:00:00\",\n  \"EntryNotAfter\": \"10:30:00\",\n  \"MinBreakoutVolMult\": 0.5,\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 1.0,\n  \"DayMultiplierWed\": 1.0,\n  \"DayMultiplierThu\": 1.0,\n  \"DayMultiplierFri\": 1.0,\n  \"CostModelRoundTripPct\": 0.10\n}", "confluenceorblong", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"TargetMultiplier\": 1.0,\n  \"FixedStopLoss\": 1000,\n  \"FixedTarget\": 0,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 1.0,\n  \"MaxTradesPerDay\": 10,\n  \"MaxCapitalPerTrade\": 100000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Buy-the-dip: uptrending stocks (2–10% above their 20-day SMA) that gapped DOWN ≥1.5%, entered when price reclaims the 9-EMA intraday (pullback touch + bullish close), filtered to moderate trend strength (ADX ≤ 25 — high ADX = exhausted move). SL at trigger low, 1.5R target, hard exit 15:00 IST. The gap-down-in-uptrend is the stock selection; the 9-EMA reclaim is the trigger. Tuned on 500 NSE stocks × 250 days (net +52k, PF ~4.5, 77% win, max drawdown 3% — IN-SAMPLE, paper-test before live; the ADX≤25 cut is data-selected and will regress somewhat).", true, "EMA Gap-Down Reclaim (Long)", "{\n  \"FastEmaPeriod\": 9,\n  \"SlowEmaPeriod\": 20,\n  \"SlopeLookback\": 5,\n  \"MinEmaDistanceAtr\": 0.3,\n  \"MaxEmaDistanceAtr\": 1.5,\n  \"MinDailyAtrPct\": 1.5,\n  \"DailyAtrPeriod\": 14,\n  \"MinDailyTrendPct\": 2.0,\n  \"MaxDailyTrendPct\": 10.0,\n  \"DailyTrendSmaPeriod\": 20,\n  \"IntradayAtrPeriod\": 14,\n  \"MorningStart\": \"10:00:00\",\n  \"MorningEnd\": \"11:00:00\",\n  \"AfternoonStart\": \"13:30:00\",\n  \"AfternoonEnd\": \"14:00:00\",\n  \"RequireEngulfing\": true,\n  \"MinStopDistancePct\": 0.45,\n  \"MaxStopDistancePct\": 1.5,\n  \"MinRvol\": 0,\n  \"RvolLookbackDays\": 10,\n  \"MinAdx\": 0,\n  \"MaxAdx\": 25,\n  \"AdxPeriod\": 14,\n  \"MinTriggerVolMult\": 0,\n  \"MinGapPct\": 0,\n  \"MaxGapPct\": 5,\n  \"MaxEntryGapPct\": -1.5,\n  \"MinPrice\": 100,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinHistoricalDays\": 25\n}", "emapullback", "{\n  \"RiskRewardRatio\": 1.5,\n  \"HardExitTime\": \"15:00:00\",\n  \"UseTrailingStop\": false,\n  \"TrailActivateR\": 1.0,\n  \"TrailGapR\": 1.0,\n  \"TrailHardTargetR\": 0,\n  \"CostModelRoundTripPct\": 0.10\n}", "emapullback", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
