using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedVwapBouncePreset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[] { 8, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Trend-continuation: a liquid (≥1cr shares/day), higher-priced (≥₹300) stock in an established intraday uptrend dips back to touch the session VWAP and closes back above it (morning window, slope not falling). Ridden with a VWAP-trailing exit — square off on the first close back below VWAP. SL = trigger (bounce) low; hard exit 15:00 IST. Tuned on 414 NSE stocks × ~480 days, 5-min, fully diagnostic-driven: the broad signal is gross-negative; the edge appears only after stacking the four filters above. Champion slice net +0.74R/trade (~₹367 at ₹500 risk), 84% of months positive, ~1.6 signals/day across the universe. Temperament is the inverse of the EMA preset — 31% win, fat-tailed (12% of trades >4R carry it). IN-SAMPLE; paper-test before live.", true, "VWAP Bounce (Long)", "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": true,\n  \"RequireRisingVwap\": false,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"MinPrice\": 300,\n  \"MinAverageDailyVolume\": 10000000,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}", "vwapbounce", "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 0,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}", "vwapbounce", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8);
        }
    }
}
