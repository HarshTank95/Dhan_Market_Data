using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCredentials",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    ClientId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    AccessTokenEncrypted = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCredentials", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StrategyPresets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsBuiltIn = table.Column<bool>(type: "INTEGER", nullable: false),
                    ScreenerType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    StrategyType = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ScreenerConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    StrategyConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    TradingConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StrategyPresets", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BacktestRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    StrategyPresetId = table.Column<int>(type: "INTEGER", nullable: false),
                    PresetSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    StockCount = table.Column<int>(type: "INTEGER", nullable: false),
                    BacktestDays = table.Column<int>(type: "INTEGER", nullable: false),
                    Timeframe = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    ExchangeSegment = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FinishedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    TotalDaysProcessed = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalDaysPlanned = table.Column<int>(type: "INTEGER", nullable: false),
                    TradeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalPnL = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BacktestRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BacktestRuns_StrategyPresets_StrategyPresetId",
                        column: x => x.StrategyPresetId,
                        principalTable: "StrategyPresets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TradeRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    BacktestRunId = table.Column<int>(type: "INTEGER", nullable: false),
                    Symbol = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    SecurityId = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntryTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EntryPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    StopLoss = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    Target = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ExitTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    ExitReason = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PnL = table.Column<decimal>(type: "TEXT", precision: 18, scale: 4, nullable: false),
                    PnLPercent = table.Column<decimal>(type: "TEXT", precision: 8, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TradeRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TradeRecords_BacktestRuns_BacktestRunId",
                        column: x => x.BacktestRunId,
                        principalTable: "BacktestRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "StrategyPresets",
                columns: new[] { "Id", "CreatedAt", "Description", "IsBuiltIn", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType", "TradingConfigJson", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Early-morning unusual volume; enter at 9:30 open with fixed SL/target.", true, "Volume Spike", "{\n  \"ScreeningCandleCount\": 3,\n  \"VolumeMultiplier\": 2.0,\n  \"CandleSizeMultiplier\": 3.0\n}", "volumespike", "{}", "fixedtarget", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 2, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Identify dominance candle in 9:30–10:00 window; enter on next-candle breakout above its high; fixed SL/target.", true, "Dominance Breakout", "{\n  \"MinBodyPercent\": 70,\n  \"MaxBodyPercent\": 85,\n  \"MinWickPercent\": 5,\n  \"MinCandleSizeMultiplier\": 1.0,\n  \"MaxCandleSizeMultiplier\": 2.5,\n  \"VolumeMultiplier\": 2.0,\n  \"MinAbsoluteVolume\": 5000,\n  \"MaxMovementMultiplier\": 2.0,\n  \"MaxGapUpPercent\": 2.5,\n  \"MaxGapDownPercent\": 1.0,\n  \"HistoricalDays\": 10,\n  \"EntryBracketStart\": \"09:30:00\",\n  \"EntryBracketEnd\": \"10:00:00\"\n}", "dominancecandle", "{}", "breakoutentry", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 3, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Same dominance-candle entry as Dominance Breakout, but trailing SL instead of fixed target.", true, "Dominance Trailing", "{\n  \"MinBodyPercent\": 70,\n  \"MaxBodyPercent\": 85,\n  \"MinWickPercent\": 5,\n  \"MinCandleSizeMultiplier\": 1.0,\n  \"MaxCandleSizeMultiplier\": 2.5,\n  \"VolumeMultiplier\": 2.0,\n  \"MinAbsoluteVolume\": 5000,\n  \"MaxMovementMultiplier\": 2.0,\n  \"MaxGapUpPercent\": 2.5,\n  \"MaxGapDownPercent\": 1.0,\n  \"HistoricalDays\": 10,\n  \"EntryBracketStart\": \"09:30:00\",\n  \"EntryBracketEnd\": \"10:00:00\"\n}", "dominancecandle", "{}", "trailingstop", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 4, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Clean gap-up + opening-range structure; enter on break above OR.High inside the execution window.", true, "Opening Range Breakout", "{\n  \"MinGapPercent\": 0.8,\n  \"MaxGapPercent\": 10.0,\n  \"MaxUpperWickPercent\": 80,\n  \"MinVolumeMultiplier\": 1.5,\n  \"CleanCandleCount\": 2,\n  \"OpeningRangeMinutes\": 10,\n  \"ObservationEndTime\": \"09:25:00\",\n  \"ExecutionWindowStart\": \"09:40:00\",\n  \"ExecutionWindowEnd\": \"09:40:00\",\n  \"HistoricalDaysForAverage\": 10,\n  \"MaxCandleSizeMultiplier\": 3.0\n}", "openingrange", "{}", "openingrange", "{\n  \"MarketOpenTime\": \"09:15:00\",\n  \"MarketCloseTime\": \"15:30:00\",\n  \"EntryTime\": \"09:30:00\",\n  \"ExitTime\": \"15:15:00\",\n  \"TargetMultiplier\": 2.5,\n  \"FixedStopLoss\": 500,\n  \"FixedTarget\": 2000,\n  \"RequireCloseAboveDayOpen\": false,\n  \"TrailStepMultiplier\": 2.0,\n  \"MaxTradesPerDay\": 2,\n  \"MaxCapitalPerTrade\": 300000\n}", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_CreatedAt",
                table: "BacktestRuns",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_Status",
                table: "BacktestRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BacktestRuns_StrategyPresetId",
                table: "BacktestRuns",
                column: "StrategyPresetId");

            migrationBuilder.CreateIndex(
                name: "IX_StrategyPresets_Name",
                table: "StrategyPresets",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TradeRecords_BacktestRunId_Date",
                table: "TradeRecords",
                columns: new[] { "BacktestRunId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TradeRecords_BacktestRunId_ExitReason",
                table: "TradeRecords",
                columns: new[] { "BacktestRunId", "ExitReason" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCredentials");

            migrationBuilder.DropTable(
                name: "TradeRecords");

            migrationBuilder.DropTable(
                name: "BacktestRuns");

            migrationBuilder.DropTable(
                name: "StrategyPresets");
        }
    }
}
