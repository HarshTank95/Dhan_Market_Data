# Strategies

A "strategy" in this app is a `(screener, execution)` combo wrapped in a named preset. There are **4 built-in presets** seeded into SQLite on first run; users can clone or create their own.

The screener decides *which stocks qualify*. The execution strategy decides *how to enter/exit*.

## Built-in presets

| Preset | Screener | Execution | One-liner |
|---|---|---|---|
| **Volume Spike** | `volumespike` | `fixedtarget` | Early-morning unusual volume; enter at 9:30 open with fixed SL/target |
| **Dominance Breakout** | `dominancecandle` | `breakoutentry` | Find a dominance candle 9:30–10:00; enter on next-candle break above its high; fixed SL/target |
| **Dominance Trailing** | `dominancecandle` | `trailingstop` | Same entry as above; trailing SL replaces fixed target |
| **Opening Range Breakout** | `openingrange` | `openingrange` | Clean gap-up + opening-range structure; enter on break above OR.High in execution window |

Seed values come from the legacy `appsettings.json` (the actively-tuned defaults).

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

## Shared trading config (per-preset, overridable)

`MarketOpenTime 09:15 · MarketCloseTime 15:30 · EntryTime 09:30 · ExitTime 15:15 · FixedStopLoss ₹500 · FixedTarget ₹2000 · TargetMultiplier 2.5 · TrailStepMultiplier 2.0 · MaxTradesPerDay 2 · MaxCapitalPerTrade ₹3,00,000`

## Built-in lock + edits

- Built-in presets cannot be edited or deleted. The UI shows **Reset to defaults** (re-seeds the original values) and **Clone** (creates a user-editable copy).
- User presets support full CRUD.

## Adding a new strategy

See `extending.md`.
