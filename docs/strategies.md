# Strategies

A "strategy" in this app is a `(screener, execution)` combo wrapped in a named preset. There are **7 built-in presets** seeded into SQLite on first run; users can clone or create their own.

The screener decides *which stocks qualify*. The execution strategy decides *how to enter/exit*.

## Built-in presets

| Preset | Screener | Execution | One-liner |
|---|---|---|---|
| **Volume Spike** | `volumespike` | `fixedtarget` | Early-morning unusual volume; enter at 9:30 open with fixed SL/target |
| **Dominance Breakout** | `dominancecandle` | `breakoutentry` | Find a dominance candle 9:30–10:00; enter on next-candle break above its high; fixed SL/target |
| **Dominance Trailing** | `dominancecandle` | `trailingstop` | Same entry as above; trailing SL replaces fixed target |
| **Opening Range Breakout** | `openingrange` | `openingrange` | Clean gap-up + opening-range structure; enter on break above OR.High in execution window |
| **Gap Fade (Long)** | `gapfade` | `gapfadelong` | Quiet, ATR-normalized gap-downs on liquid trending stocks; confirmation-candle mean-reversion entry (see `1_strategy-rvol-orb.md` family) |
| **Volume Confluence Breakout (Long)** | `rvolorb` | `confluenceorblong` | F&O 15-min ORB filtered by cash RVOL + futures OI direction (see `1_strategy-rvol-orb.md`, `2_strategy-rvol-orb-integration.md`) |
| **EMA Gap-Down Reclaim (Long)** | `emapullback` | `emapullback` | Buy-the-dip: uptrending stock (2–10% above 20-day SMA) that gapped down ≥1.5%, entered on the intraday 9-EMA reclaim |

Seed values come from the legacy `appsettings.json` (the actively-tuned defaults), except the EMA Gap-Down Reclaim preset, whose defaults were tuned empirically in-app (see its section below).

## Screeners

### Volume Spike (`volumespike`, `VolumeSpikeConfig`)
- All first N candles (default 3) green and high-volume
- Volume ≥ `VolumeMultiplier` × historical average
- Candle size < `CandleSizeMultiplier` × historical average

### Breakout (`breakout`, `BreakoutConfig`)
- Close ≥ historicalLow + range × `BreakoutThreshold`
- Green candle with volume ≥ `VolumeMultiplier` × average

### Dominance Candle (`dominancecandle`, `DominanceCandleConfig`)
- Body 70–85% of range (`MinBodyPercent` … `MaxBodyPercent`)
- Both wicks ≥ 5% of range (`MinWickPercent`)
- Candle size 1.0–2.5× 10-day average
- Volume ≥ 2.0× average AND ≥ `MinAbsoluteVolume` (default 5000)
- Gap filter: ≤ 2.5% gap-up, ≤ 1.0% gap-down
- Within IST entry bracket (default 09:30–10:00)
- Total move ≤ `MaxMovementMultiplier` × expected (filters explosive opens)

### Opening Range (`openingrange`, `OpeningRangeConfig`)
- Gap-up between `MinGapPercent` and `MaxGapPercent`
- First N "clean" candles (low upper-wick fraction)
- Volume ≥ `MinVolumeMultiplier` × average
- Opening range ends at `ObservationEndTime` (default 09:25)
- Breakout entry must occur within `ExecutionWindowStart`–`End`

### EMA Gap-Down Reclaim (`emapullback`, `EmaPullbackScreenerConfig`)
> **The edge is stock selection, not the EMA.** A buy-the-dip: the screener
> only fires on uptrending stocks that gapped *down*, where price reclaims the
> 9-EMA intraday. Requires daily candles. Returns the reclaim candle as the
> signal (its low is the strategy's stop). Diagnostic columns are repurposed:
> `RvolAtEntry`=RVOL, `OrWidthPct`=ADX, `GapPct`=gap%.

Per-stock-day gates (cheap → expensive):
- **Liquidity:** price ≥ `MinPrice` (100), 20-day avg volume ≥ `MinAverageDailyVolume`
- **Volatility:** daily ATR(14) / price ≥ `MinDailyAtrPct` (1.5%)
- **Daily trend band:** prevClose vs SMA(`DailyTrendSmaPeriod`=20) in `[MinDailyTrendPct, MaxDailyTrendPct]` = **2–10% above** — confirmed uptrend, not overextended
- **Gap selection:** today's open gap ≤ `MaxEntryGapPct` (**−1.5%** ⇒ gapped down ≥1.5%) — *the dominant filter*

Per-candle trigger (first qualifying candle in a time window wins):
- Time window: `[MorningStart, MorningEnd]` (10:00–11:00) or `[AfternoonStart, AfternoonEnd]` (13:30–14:00) IST
- 9-EMA > 20-EMA, 20-EMA rising over `SlopeLookback` (5), EMA gap in `[MinEmaDistanceAtr, MaxEmaDistanceAtr]` × intraday ATR
- Candle touches the 9-EMA, closes above it, bullish body; with `RequireEngulfing`, close > prior candle high
- Stop distance (close−low)/close in `[MinStopDistancePct, MaxStopDistancePct]` (0.45–1.5%) — filters chop and caps position size

Optional dials (default off): `MinRvol`, `MinAdx`/`MaxAdx`, `MinTriggerVolMult`. Empirically RVOL didn't help and **ADX≥25 hurt** (high ADX = exhausted move); a `MaxAdx≤25` *quality* variant trades far fewer but at ~78% win / PF ~4.5 in-sample (overfit risk — paper-test before trusting).

### Gap Fade & Volume Confluence
See `1_strategy-rvol-orb.md` and `2_strategy-rvol-orb-integration.md`.

## Execution strategies

### Fixed Target (`fixedtarget`)
- Entry at `entryCandle.Open` (after screener fires)
- Stop = lowest low among the screener's signal candles
- Target = entry + (`FixedTarget` / quantity), where `Quantity = floor(FixedStopLoss / riskPerShare)`
- Time-stop at `ExitTime` (default 15:15 IST)

### Breakout Entry (`breakoutentry`)
- Used with `dominancecandle`. Enter at `dominance.High` only if the **next** candle breaks above it
- Stop at `dominance.Low`, fixed-rupee target, 15:15 time-stop

### Trailing Stop (`trailingstop`)
- Same entry as Breakout Entry
- Stop trails up by `FixedStopLoss × TrailStepMultiplier` per profit step (default 2.0× ⇒ ₹1000 step)
- No fixed target — runs to trail-out or 15:15

### Opening Range Breakout (`openingrange`)
- Enter at `OR.High` if a candle breaks above inside the execution window
- Stop at `OR.Low`, fixed-rupee target, 15:15 time-stop

### EMA Gap-Down Reclaim (`emapullback`, `EmaPullbackStrategyConfig`)
- Entry at the **open of the candle after** the screener's reclaim candle
- Stop = reclaim candle's **low**; `Quantity = floor(FixedStopLoss / riskPerShare)`
- Target = entry + `RiskRewardRatio` × risk (default **1.5R**)
- Hard time-stop at `HardExitTime` (15:00 IST)
- Optional `UseTrailingStop` (chandelier: arm at `TrailActivateR`, trail `TrailGapR` below the high) — tested and *worse* than the fixed 1.5R on 5-min, so default **off**
- P&L is net of `CostModelRoundTripPct` (0.10% round-trip)

**Tuning history (in-sample, 500 NSE stocks × 250 days, 5-min):** the raw "EMA pullback on all stocks" lost (−95k, PF 0.5) — cost ate the thin edge. Adding the daily-trend band, a min-stop floor, and tighter time windows reached breakeven; the **gap-down ≤ −1.5% selection** was the breakthrough (net **+66k, PF ~1.8, 58% win, all 13 months positive, max drawdown 7% of profit**). This is the locked-in default. Still **in-sample — paper-trade before risking capital**; gap-down opens can slip more than the modeled 0.10%.

## Shared trading config (per-preset, overridable)

`MarketOpenTime 09:15 · MarketCloseTime 15:30 · EntryTime 09:30 · ExitTime 15:15 · FixedStopLoss ₹500 · FixedTarget ₹2000 · TargetMultiplier 2.5 · TrailStepMultiplier 2.0 · MaxTradesPerDay 2 · MaxCapitalPerTrade ₹3,00,000`

## Built-in lock + edits

- Built-in presets cannot be edited or deleted. The UI shows **Reset to defaults** (re-seeds the original values) and **Clone** (creates a user-editable copy).
- User presets support full CRUD.

## Adding a new strategy

See `extending.md`.
