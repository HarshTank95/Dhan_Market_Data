using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedEmaPullbackPreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[] { 7, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "9/20 EMA stacked + rising 20 EMA; pullback touches 9 EMA with a bullish close inside the morning (09:30-11:00) or early-afternoon (13:30-14:45) window. Enter next bar's open, SL at trigger low, 1.5R target, hard exit 15:00 IST.", true, "EMA Pullback Continuation", "{\n  \"FastEmaPeriod\": 9,\n  \"SlowEmaPeriod\": 20,\n  \"SlopeLookback\": 5,\n  \"MinEmaDistanceAtr\": 0.3,\n  \"MaxEmaDistanceAtr\": 1.5,\n  \"MinDailyAtrPct\": 1.5,\n  \"DailyAtrPeriod\": 14,\n  \"IntradayAtrPeriod\": 14,\n  \"MorningStart\": \"09:30:00\",\n  \"MorningEnd\": \"11:00:00\",\n  \"AfternoonStart\": \"13:30:00\",\n  \"AfternoonEnd\": \"14:45:00\",\n  \"RequireEngulfing\": true,\n  \"MinPrice\": 50,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinHistoricalDays\": 25\n}", "emapullback", "{\n  \"RiskRewardRatio\": 1.5,\n  \"HardExitTime\": \"15:00:00\",\n  \"CostModelRoundTripPct\": 0.10\n}", "emapullback", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 7);
        }
    }
}
