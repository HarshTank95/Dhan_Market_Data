using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenGenerationSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PinEncrypted",
                table: "ApiCredentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TokenExpiresAt",
                table: "ApiCredentials",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TotpSeedEncrypted",
                table: "ApiCredentials",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PinEncrypted",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "TokenExpiresAt",
                table: "ApiCredentials");

            migrationBuilder.DropColumn(
                name: "TotpSeedEncrypted",
                table: "ApiCredentials");
        }
    }
}
