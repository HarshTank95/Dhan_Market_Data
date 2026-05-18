using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateRvolOrbPresetWithCostModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "StrategyConfigJson" },
                values: new object[] { "15-min ORB on F&O-eligible NSE stocks, filtered by cash RVOL and confirmed by futures OI direction. Long buildup = full size; short covering = half size. v1 uses 5-min timeframe (15-min OI delta requires multi-candle OR), MinScoreThreshold instead of cross-stock top-N ranking, and includes spec §12 cost model (0.10% RT).", "{\n  \"AtrStopMultiplier\": 0.15,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 0.5,\n  \"DayMultiplierWed\": 0.8,\n  \"DayMultiplierThu\": 1.2,\n  \"DayMultiplierFri\": 1.5,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 6,
                columns: new[] { "Description", "StrategyConfigJson" },
                values: new object[] { "15-min ORB on F&O-eligible NSE stocks, filtered by cash RVOL and confirmed by futures OI direction. Long buildup = full size; short covering = half size.", "{\n  \"AtrStopMultiplier\": 0.15,\n  \"NoFillCutoff\": \"13:00:00\",\n  \"ExitTime\": \"14:30:00\",\n  \"DayMultiplierMon\": 1.0,\n  \"DayMultiplierTue\": 0.5,\n  \"DayMultiplierWed\": 0.8,\n  \"DayMultiplierThu\": 1.2,\n  \"DayMultiplierFri\": 1.5\n}" });
        }
    }
}
