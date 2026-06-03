using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DhanMarketData.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HonestVwapBouncePresetDescription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "EXPERIMENTAL — does NOT meet the playbook robustness gates. Trend-continuation idea: a liquid (≥1cr shares/day prior avg), higher-priced (≥₹300) stock in an established intraday uptrend dips back to touch the session VWAP and closes back above it (morning window, slope not falling); VWAP-trailing exit. SL = trigger (bounce) low; hard exit 15:00 IST. Original lookahead-tainted diagnostic (today's-full-volume filter) promised +0.74R/trade / 84% months positive. Corrected NON-LOOKAHEAD diagnostic (prior-20-day avg vol + first-bounce-only, matching the live screener) reveals the real edge is ~+0.21R/trade with only 46% months positive — marginal, and the in-app preset run (500 stocks × 250d) was actually net-NEGATIVE on the recent window. Kept as a research artifact + cautionary tale (a diagnostic must use the same non-lookahead constraints as the live screener, or it over-promises). NOT recommended for live trading as-is.");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "StrategyPresets",
                keyColumn: "Id",
                keyValue: 8,
                column: "Description",
                value: "Trend-continuation: a liquid (≥1cr shares/day), higher-priced (≥₹300) stock in an established intraday uptrend dips back to touch the session VWAP and closes back above it (morning window, slope not falling). Ridden with a VWAP-trailing exit — square off on the first close back below VWAP. SL = trigger (bounce) low; hard exit 15:00 IST. Tuned on 414 NSE stocks × ~480 days, 5-min, fully diagnostic-driven: the broad signal is gross-negative; the edge appears only after stacking the four filters above. Champion slice net +0.74R/trade (~₹367 at ₹500 risk), 84% of months positive, ~1.6 signals/day across the universe. Temperament is the inverse of the EMA preset — 31% win, fat-tailed (12% of trades >4R carry it). IN-SAMPLE; paper-test before live.");
        }
    }
}
