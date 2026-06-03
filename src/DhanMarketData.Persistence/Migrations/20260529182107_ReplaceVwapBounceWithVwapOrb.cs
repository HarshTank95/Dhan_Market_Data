using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceVwapBounceWithVwapOrb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType" },
                values: new object[] { "Momentum: on a trending Mon/Wed session, a liquid (≥30L/day prior avg), higher-priced (≥₹500) stock that gapped ≥0% breaks above its 30-min opening-range high while holding above a RISING session VWAP (slope 20–50 bps — with-flow but not exhausted) and the opening range was wide (≥1%). Enter next bar's open; SL = min(VWAP, breakout-bar low); HELD TO 15:00 IST (only the stop exits earlier). The day + liquidity + price + OR-width + slope-band + gap selection is the edge; the opening-range break is the trigger. Developed via the offline diagnostic harness on 414 NSE stocks × ~250 days (5-min), fully non-lookahead: raw signal was gross-negative; this stacked config reached ~88 trades, +0.93R/trade (~₹466 at ₹500 risk), 63% win, 11/13 months positive. Mon/Wed avoids Tue/Thu/Fri expiry-day chop (corroborated across two VWAP strategies). IN-SAMPLE — paper-test before live.", "VWAP ORB Momentum (Long)", "{\n  \"OpeningRangeBars\": 6,\n  \"MinOrWidthPct\": 1.0,\n  \"VwapSlopeLookback\": 3,\n  \"MinVwapSlopeBps\": 20,\n  \"MaxVwapSlopeBps\": 50,\n  \"MinGapPct\": 0,\n  \"WindowStart\": \"09:45:00\",\n  \"WindowEnd\": \"14:00:00\",\n  \"AllowMon\": true,\n  \"AllowTue\": false,\n  \"AllowWed\": true,\n  \"AllowThu\": false,\n  \"AllowFri\": false,\n  \"MinStopDistancePct\": 0.5,\n  \"MaxStopDistancePct\": 0,\n  \"MinPrice\": 500,\n  \"MinAverageDailyVolume\": 3000000,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}", "vwaporb", "{\n  \"HardExitTime\": \"15:00:00\",\n  \"ExitOnCloseBelowVwap\": false,\n  \"HardTargetR\": 0,\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}", "vwaporb" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "Description", "Name", "ScreenerConfigJson", "ScreenerType", "StrategyConfigJson", "StrategyType" },
                values: new object[] { "EXPERIMENTAL — does NOT meet the playbook robustness gates. Trend-continuation idea: a liquid (≥1cr shares/day prior avg), higher-priced (≥₹300) stock in an established intraday uptrend dips back to touch the session VWAP and closes back above it (morning window, slope not falling); VWAP-trailing exit. SL = trigger (bounce) low; hard exit 15:00 IST. Original lookahead-tainted diagnostic (today's-full-volume filter) promised +0.74R/trade / 84% months positive. Corrected NON-LOOKAHEAD diagnostic (prior-20-day avg vol + first-bounce-only, matching the live screener) reveals the real edge is ~+0.21R/trade with only 46% months positive — marginal, and the in-app preset run (500 stocks × 250d) was actually net-NEGATIVE on the recent window. Kept as a research artifact + cautionary tale (a diagnostic must use the same non-lookahead constraints as the live screener, or it over-promises). NOT recommended for live trading as-is.", "VWAP Bounce (Long)", "{\n  \"VwapSlopeLookback\": 3,\n  \"RejectFallingVwap\": false,\n  \"RequireRisingVwap\": false,\n  \"RejectFlatZoneMaxBps\": 0,\n  \"FlatZoneTightStopAdmitPct\": 0,\n  \"MinAboveVwapFraction\": 0.6,\n  \"UptrendVwapLookback\": 6,\n  \"MinWarmupBars\": 7,\n  \"VwapTouchBufferPct\": 0.1,\n  \"RequireBullishCandle\": true,\n  \"MinStopDistancePct\": 0.3,\n  \"MaxStopDistancePct\": 1.5,\n  \"WindowStart\": \"09:30:00\",\n  \"WindowEnd\": \"12:00:00\",\n  \"AllowThursday\": true,\n  \"AllowFriday\": true,\n  \"MinPrice\": 100,\n  \"MinAverageDailyVolume\": 1000000,\n  \"MaxAverageDailyVolume\": 0,\n  \"VolumeLookbackDays\": 20,\n  \"MinHistoricalDays\": 20\n}", "vwapbounce", "{\n  \"ExitOnCloseBelowVwap\": true,\n  \"MinHoldBars\": 0,\n  \"HardTargetR\": 0,\n  \"HardExitTime\": \"15:00:00\",\n  \"RiskPerTrade\": 500,\n  \"CostModelRoundTripPct\": 0.10\n}", "vwapbounce" });
        }
    }
}
