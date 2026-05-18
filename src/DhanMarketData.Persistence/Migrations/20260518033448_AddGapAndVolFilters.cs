using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGapAndVolFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.0,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": false,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28,\n  \"SkipTuesday\": true,\n  \"MinGapPct\": -1.5\n}", "{\n  \"AtrStopMultiplier\": 0.30,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"EntryNotBefore\": \"10:00:00\",\n  \"EntryNotAfter\": \"10:30:00\",\n  \"MinBreakoutVolMult\": 0.5,\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 1.0,\n  \"DayMultiplierWed\": 1.0,\n  \"DayMultiplierThu\": 1.0,\n  \"DayMultiplierFri\": 1.0,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "{\n  \"OpeningRangeMinutes\": 15,\n  \"DojiThreshold\": 0.10,\n  \"RvolLookbackDays\": 14,\n  \"MinRvol\": 1.0,\n  \"MinScoreThreshold\": 1.0,\n  \"RequireFnoOnly\": true,\n  \"MinPrice\": 50,\n  \"MinAvgRupeeVolume\": 1000000000,\n  \"MinAtrPercent\": 1.0,\n  \"MaxYesterdayRangePct\": 9.0,\n  \"AtrLookback\": 14,\n  \"RequireOiConfluence\": false,\n  \"MinOiDeltaPercent\": 1.0,\n  \"SkipDayIfIndiaVixAbove\": 25.0,\n  \"SkipDayIfNiftyGapPct\": 2.0,\n  \"MinHistoricalDays\": 28,\n  \"SkipTuesday\": true\n}", "{\n  \"AtrStopMultiplier\": 0.30,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"EntryNotBefore\": \"10:00:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 1.0,\n  \"DayMultiplierWed\": 1.0,\n  \"DayMultiplierThu\": 1.0,\n  \"DayMultiplierFri\": 1.0,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }
    }
}
