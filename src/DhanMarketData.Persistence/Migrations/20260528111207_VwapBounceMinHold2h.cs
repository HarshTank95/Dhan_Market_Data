using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VwapBounceMinHold2h : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "StrategyConfigJson",
                value: "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 24,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "StrategyConfigJson",
                value: "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 0,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}");
        }
    }
}
