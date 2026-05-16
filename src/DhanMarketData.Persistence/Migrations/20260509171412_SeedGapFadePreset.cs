using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedGapFadePreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[] { 5, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Quiet, ATR-normalized gap-downs on liquid trending stocks; confirmation-candle entry inside 09:30–10:15 window; mean-reversion long.", true, "Gap Fade (Long)", "{\n  \"MinGapAtrRatio\": 0.20,\n  \"MaxGapAtrRatio\": 0.60,\n  \"RequirePartialGap\": true,\n  \"RequireUnfilledAtEntry\": true,\n  \"MaxOpeningVolumeMultiplier\": 0.8,\n  \"MinAbsoluteVolume\": 1000,\n  \"VolumeAverageDays\": 10,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinPrice\": 100,\n  \"RequireUptrend\": true,\n  \"SmaPeriod\": 20,\n  \"AtrPeriod\": 14,\n  \"MinHistoricalDays\": 25,\n  \"MaxStopLossPercent\": 1.0\n}", "gapfade", "{\n  \"EntryWindowStart\": \"09:30:00\",\n  \"EntryWindowEnd\": \"10:15:00\",\n  \"RequireConfirmationCandle\": true,\n  \"TimeExit\": \"12:30:00\"\n}", "gapfadelong", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 5);
        }
    }
}
