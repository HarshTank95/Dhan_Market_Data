using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedRvolOrbPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[] { 6, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "15-min ORB on F&O-eligible NSE stocks, filtered by cash RVOL and confirmed by futures OI direction. Long buildup = full size; short covering = half size.", true, "Volume Confluence Breakout (Long)", "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.5,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": true,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28\n}", "rvolorb", "{\n  \"AtrStopMultiplier\": 0.15,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 0.5,\n  \"DayMultiplierWed\": 0.8,\n  \"DayMultiplierThu\": 1.2,\n  \"DayMultiplierFri\": 1.5\n}", "confluenceorblong", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"TargetMultiplier\": 1.0,\n  \"FixedStopLoss\": 1000,\n  \"FixedTarget\": 0,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 1.0,\n  \"MaxTradesPerDay\": 10,\n  \"MaxCapitalPerTrade\": 100000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6);
        }
    }
}
