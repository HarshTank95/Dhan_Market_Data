using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameTuneEmaGapDownReclaim : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name", "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "Buy-the-dip: uptrending stocks (2–10% above their 20-day SMA) that gapped DOWN ≥1.5%, entered when price reclaims the 9-EMA intraday (pullback touch + bullish close), filtered to moderate trend strength (ADX ≤ 25 — high ADX = exhausted move). SL at trigger low, 1.5R target, hard exit 15:00 IST. The gap-down-in-uptrend is the stock selection; the 9-EMA reclaim is the trigger. Tuned on 500 NSE stocks × 250 days (net +52k, PF ~4.5, 77% win, max drawdown 3% — IN-SAMPLE, paper-test before live; the ADX≤25 cut is data-selected and will regress somewhat).", "EMA Gap-Down Reclaim (Long)", "{\n  \"FastEmaPeriod\": 9,\n  \"SlowEmaPeriod\": 20,\n  \"SlopeLookback\": 5,\n  \"MinEmaDistanceAtr\": 0.3,\n  \"MaxEmaDistanceAtr\": 1.5,\n  \"MinDailyAtrPct\": 1.5,\n  \"DailyAtrPeriod\": 14,\n  \"MinDailyTrendPct\": 2.0,\n  \"MaxDailyTrendPct\": 10.0,\n  \"DailyTrendSmaPeriod\": 20,\n  \"IntradayAtrPeriod\": 14,\n  \"MorningStart\": \"10:00:00\",\n  \"MorningEnd\": \"11:00:00\",\n  \"AfternoonStart\": \"13:30:00\",\n  \"AfternoonEnd\": \"14:00:00\",\n  \"RequireEngulfing\": true,\n  \"MinStopDistancePct\": 0.45,\n  \"MaxStopDistancePct\": 1.5,\n  \"MinRvol\": 0,\n  \"RvolLookbackDays\": 10,\n  \"MinAdx\": 0,\n  \"MaxAdx\": 25,\n  \"AdxPeriod\": 14,\n  \"MinTriggerVolMult\": 0,\n  \"MinGapPct\": 0,\n  \"MaxGapPct\": 5,\n  \"MaxEntryGapPct\": -1.5,\n  \"MinPrice\": 100,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinHistoricalDays\": 25\n}", "{\n  \"RiskRewardRatio\": 1.5,\n  \"HardExitTime\": \"15:00:00\",\n  \"UseTrailingStop\": false,\n  \"TrailActivateR\": 1.0,\n  \"TrailGapR\": 1.0,\n  \"TrailHardTargetR\": 0,\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 7,
                columns: new[] { "Description", "Name", "ScreenerConfigJson", "StrategyConfigJson" },
                values: new object[] { "9/20 EMA stacked + rising 20 EMA; pullback touches 9 EMA with a bullish close inside the morning (09:30-11:00) or early-afternoon (13:30-14:45) window. Enter next bar's open, SL at trigger low, 1.5R target, hard exit 15:00 IST.", "EMA Pullback Continuation", "{\n  \"FastEmaPeriod\": 9,\n  \"SlowEmaPeriod\": 20,\n  \"SlopeLookback\": 5,\n  \"MinEmaDistanceAtr\": 0.3,\n  \"MaxEmaDistanceAtr\": 1.5,\n  \"MinDailyAtrPct\": 1.5,\n  \"DailyAtrPeriod\": 14,\n  \"IntradayAtrPeriod\": 14,\n  \"MorningStart\": \"09:30:00\",\n  \"MorningEnd\": \"11:00:00\",\n  \"AfternoonStart\": \"13:30:00\",\n  \"AfternoonEnd\": \"14:45:00\",\n  \"RequireEngulfing\": true,\n  \"MinPrice\": 50,\n  \"MinAverageDailyVolume\": 500000,\n  \"MinHistoricalDays\": 25\n}", "{\n  \"RiskRewardRatio\": 1.5,\n  \"HardExitTime\": \"15:00:00\",\n  \"CostModelRoundTripPct\": 0.10\n}" });
        }
    }
}
