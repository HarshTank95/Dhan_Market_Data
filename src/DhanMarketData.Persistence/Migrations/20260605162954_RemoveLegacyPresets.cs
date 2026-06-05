using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveLegacyPresets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The 4 legacy presets (Volume Spike, Dominance Breakout, Dominance
            // Trailing, Opening Range Breakout) are being removed entirely. The
            // BacktestRuns → StrategyPresets FK is Restrict, so any prior run
            // history must be deleted first or the preset DeleteData below fails.
            // Trades are removed first (TradeRecords → BacktestRuns), then the
            // runs, then the presets. User-confirmed: delete this run history.
            migrationBuilder.Sql(
                "DELETE FROM TradeRecords WHERE BacktestRunId IN " +
                "(SELECT Id FROM BacktestRuns WHERE StrategyPresetId IN (1, 2, 3, 4));");
            migrationBuilder.Sql(
                "DELETE FROM BacktestRuns WHERE StrategyPresetId IN (1, 2, 3, 4);");

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 4);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Early-morning unusual volume; enter at 9:30 open with fixed SL/target.", true, "Volume Spike", "{\n  \"ScreeningCandleCount\": 3,\n  \"VolumeMultiplier\": 2.0,\n  \"CandleSizeMultiplier\": 3.0\n}", "volumespike", "{}", "fixedtarget", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Identify dominance candle in 9:30–10:00 window; enter on next-candle breakout above its high; fixed SL/target.", true, "Dominance Breakout", "{\n  \"MinBodyPercent\": 70,\n  \"MaxBodyPercent\": 85,\n  \"MinWickPercent\": 5,\n  \"MinCandleSizeMultiplier\": 1.0,\n  \"MaxCandleSizeMultiplier\": 2.5,\n  \"VolumeMultiplier\": 2.0,\n  \"MinAbsoluteVolume\": 5000,\n  \"MaxMovementMultiplier\": 2.0,\n  \"MaxGapUpPercent\": 2.5,\n  \"MaxGapDownPercent\": 1.0,\n  \"HistoricalDays\": 10,\n  \"EntryBracketStart\": \"09:30:00\",\n  \"EntryBracketEnd\": \"10:00:00\"\n}", "dominancecandle", "{}", "breakoutentry", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Same dominance-candle entry as Dominance Breakout, but trailing SL instead of fixed target.", true, "Dominance Trailing", "{\n  \"MinBodyPercent\": 70,\n  \"MaxBodyPercent\": 85,\n  \"MinWickPercent\": 5,\n  \"MinCandleSizeMultiplier\": 1.0,\n  \"MaxCandleSizeMultiplier\": 2.5,\n  \"VolumeMultiplier\": 2.0,\n  \"MinAbsoluteVolume\": 5000,\n  \"MaxMovementMultiplier\": 2.0,\n  \"MaxGapUpPercent\": 2.5,\n  \"MaxGapDownPercent\": 1.0,\n  \"HistoricalDays\": 10,\n  \"EntryBracketStart\": \"09:30:00\",\n  \"EntryBracketEnd\": \"10:00:00\"\n}", "dominancecandle", "{}", "trailingstop", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clean gap-up + opening-range structure; enter on break above OR.High inside the execution window.", true, "Opening Range Breakout", "{\n  \"MinGapPercent\": 0.8,\n  \"MaxGapPercent\": 10.0,\n  \"MaxUpperWickPercent\": 80,\n  \"MinVolumeMultiplier\": 1.5,\n  \"CleanCandleCount\": 2,\n  \"OpeningRangeMinutes\": 10,\n  \"ObservationEndTime\": \"09:25:00\",\n  \"ExecutionWindowStart\": \"09:40:00\",\n  \"ExecutionWindowEnd\": \"09:40:00\",\n  \"HistoricalDaysForAverage\": 10,\n  \"MaxCandleSizeMultiplier\": 3.0\n}", "openingrange", "{}", "openingrange", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }
    }
}
