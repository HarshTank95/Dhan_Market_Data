using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FlipRvolOrbPresetOiConfluenceOff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                column: "ScreenerConfigJson",
                value: "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.5,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": false,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28\n}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                column: "ScreenerConfigJson",
                value: "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.5,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": true,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28\n}");
        }
    }
}
