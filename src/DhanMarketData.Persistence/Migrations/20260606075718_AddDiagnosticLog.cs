using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiagnosticLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DiagnosticLogEnabled",
                table: "BacktestRuns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagnosticLogEnabled",
                table: "BacktestRuns");
        }
    }
}
