using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerTradeContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "BreakoutCandleVolMult",
                table: "TradeRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GapPct",
                table: "TradeRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OrWidthPct",
                table: "TradeRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "RvolAtEntry",
                table: "TradeRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BreakoutCandleVolMult",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "GapPct",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "OrWidthPct",
                table: "TradeRecords");

            migrationBuilder.DropColumn(
                name: "RvolAtEntry",
                table: "TradeRecords");
        }
    }
}
