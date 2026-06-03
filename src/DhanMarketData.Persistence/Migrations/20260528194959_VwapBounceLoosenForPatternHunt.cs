using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VwapBounceLoosenForPatternHunt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": false,\n  \"RequireRisingVwap\": false,\n  \"RejectFlatZoneMaxBps\": 0,\n  \"FlatZoneTightStopAdmitPct\": 0,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"AllowThursday\": true,\n  \"AllowFriday\": true,\n  \"MinPrice\": 100,\n  \"MinAverageDailyVolume\": 1000000,\n  \"MaxAverageDailyVolume\": 0,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}", "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 0,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": false,\n  \"RequireRisingVwap\": false,\n  \"RejectFlatZoneMaxBps\": 10,\n  \"FlatZoneTightStopAdmitPct\": 0.35,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"AllowThursday\": false,\n  \"AllowFriday\": false,\n  \"MinPrice\": 300,\n  \"MinAverageDailyVolume\": 5000000,\n  \"MaxAverageDailyVolume\": 30000000,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}", "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 24,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }
    }
}
