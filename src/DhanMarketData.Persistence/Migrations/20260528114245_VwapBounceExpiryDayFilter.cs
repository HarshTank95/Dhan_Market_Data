using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VwapBounceExpiryDayFilter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "ScreenerConfigJson",
                value: "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": true,\n  \"RequireRisingVwap\": false,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"AllowThursday\": false,\n  \"AllowFriday\": false,\n  \"MinPrice\": 300,\n  \"MinAverageDailyVolume\": 10000000,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "ScreenerConfigJson",
                value: "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": true,\n  \"RequireRisingVwap\": false,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"MinPrice\": 300,\n  \"MinAverageDailyVolume\": 10000000,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}");
        }
    }
}
