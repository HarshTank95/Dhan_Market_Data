# Strategies

A "strategy" in this app is a `(screener, execution)` combo wrapped in a named preset. There is **1 built-in preset** (VWAP ORB Momentum) seeded into SQLite on first run; users can clone or create their own.

> **History:** the four original experiments (Volume Spike, Dominance Breakout, Dominance Trailing, Opening Range Breakout) were removed in migration `RemoveLegacyPresets` (2026-06) — they never developed a validated edge.
>
> **Retired 2026-06-07 (`RemoveCorruptedPresets`):** Gap Fade (Long), Volume Confluence Breakout (Long), and EMA Gap-Down Reclaim (Long) were removed along with their run history. All three consumed daily candles and their historical backtests were inflated by the **daily-bar look-ahead** (a daily candle for trading day *D* was stamped on *D−1*, so the screener read *today's* close as the "previous close"). After that bug was fixed (`DhanDataApiClient.GetDailyHistoricalAsync`, +5:30 IST offset) and the cache purged, all three lost their edge on clean data. Volume Confluence additionally relied on a stop-market fill **at OR.High** that isn't reachable live — ~93% of its apparent edge vanished under realistic fills (it opened above OR.High on 47% of entries). VWAP ORB was unaffected (it never used daily candles) and is the sole surviving built-in. The `breakout` screener class is retained (it had no preset) for ad-hoc custom presets.

The screener decides *which stocks qualify*. The execution strategy decides *how to enter/exit*.

## Built-in presets

| Preset | Screener | Execution | One-liner |
|---|---|---|---|
| **VWAP ORB Momentum (Long)** | `vwaporb` | `vwaporb` | Momentum: Mon/Wed liquid (≥30L/day), higher-priced (≥₹500) stock breaks above its 30-min opening-range high while holding a rising VWAP (slope 20–50 bps) on a non-negative gap day; held to 15:00. |

VWAP ORB Momentum is the only built-in preset; it was tuned empirically in-app and validated as look-ahead-free (see its sections below). The `breakout` screener has no preset but is registered for ad-hoc custom presets.

## Screeners

### Breakout (`breakout`, `BreakoutConfig`)
- Close ≥ historicalLow + range × `BreakoutThreshold`
- Green candle with volume ≥ `VolumeMultiplier` × average

### VWAP ORB Momentum (`vwaporb`, `VwapOrbScreenerConfig`)
> **The selection is the edge, not the VWAP line.** A *momentum* breakout (not the
> mean-reversion the name might suggest): on a trending Mon/Wed session, a liquid,
> higher-priced stock breaks above its opening-range high while holding above a
> RISING session VWAP whose slope is "with-flow but not exhausted". The day +
> liquidity + price + OR-width + slope-band + gap selection is the edge; the
> opening-range break is the trigger. **Intraday-only** (no daily candles / no API
> token): session VWAP each day; liquidity + gap from the prior-day intraday history.
> Diagnostics: `RvolAtEntry`=avg daily volume (M shares), `OrWidthPct`=VWAP slope
> (bps), `GapPct`=opening-range width %.

Per-stock-day gates (cheap → expensive):
- **Day-of-week:** Mon/Wed only (`AllowMon`/`AllowWed`=true). Tue/Thu/Fri lose — Nifty weekly expiry (Tue), Bank-Nifty/residual (Thu), pre-weekend (Fri) max-pain gravity wrecks trend breakouts. *Corroborated across two independent VWAP strategies.*
- **Liquidity:** prior-`VolumeLookbackDays` avg daily volume ≥ `MinAverageDailyVolume` (default **30L/day**); price ≥ `MinPrice` (default **₹500** — sub-₹500 names chop)
- **Gap:** today's open gap ≥ `MinGapPct` (default **0** — a non-negative/momentum day)
- **OR width:** opening-range (high−low)/high ≥ `MinOrWidthPct` (default **1%** — a wide OR = a volatile/trending day; narrow = chop)

Per-candle trigger (first qualifying candle in the window wins):
- Opening range = first `OpeningRangeBars` bars (default **6 = 09:15–09:45**); breakout level = OR high
- Time window: `[WindowStart, WindowEnd]` (default **09:45–14:00 IST**)
- **Fresh break:** Close > OR-high with the prior bar at/below it
- **VWAP slope band:** Close > VWAP **AND** slope over `VwapSlopeLookback` in `[MinVwapSlopeBps, MaxVwapSlopeBps]` (default **20–50 bps** — rising with the flow, but ≥50 = exhausted and fails)
- **Stop band:** stop = min(VWAP, breakout-bar low); (entry − stop)/entry ≥ `MinStopDistancePct` (default 0.5%)

> **Retired strategies:** Gap Fade, Volume Confluence (`1_strategy-rvol-orb.md`, `2_strategy-rvol-orb-integration.md`), and EMA Gap-Down Reclaim were removed 2026-06-07 — see the History note at the top. Their design docs are kept for reference but the code/presets are gone.

## Execution strategies

### VWAP ORB Momentum (`vwaporb`, `VwapOrbStrategyConfig`)
- Entry at the **open of the candle after** the screener's breakout candle
- Stop = **min(session VWAP at the breakout, breakout-bar low)**; the strategy recomputes session VWAP (same formula as the screener) so the value agrees at the shared bar. `Quantity = floor(RiskPerTrade / riskPerShare)` (or `TradingConfig.FixedStopLoss` if `RiskPerTrade=0`)
- **Exit: HOLD TO TIME** — square off at `HardExitTime` (15:00 IST); only the protective stop exits earlier. Validated: holding momentum breakouts to the close beat the VWAP-trail (which cut winners short).
- Optional dials (default off): `ExitOnCloseBelowVwap` (VWAP-trail), `HardTargetR` (profit cap).
- P&L net of `CostModelRoundTripPct` (0.10% round-trip)

**Status — net-positive and in-app validated across two years; in-sample, paper-test before live.** The first VWAP config to survive full in-app validation (the earlier bounce experiments did not).

**Development history (offline harness `tools/vwap-diag*.cs`, fully non-lookahead, then in-app validated):**
- **The dead ends first.** Every VWAP *mean-reversion* idea — bounce-to-VWAP, reclaim, −2σ/−3σ fade, short-the-rally — was net-negative after cost (≈ −₹150 to −₹320/trade on the broad set). So were the dual-VWAP cross and EMA-cross+VWAP combos, and a momentum band-breakout. No exit/stop/breakeven/partial variant rescued any of them. *Systematic VWAP as a primary signal has no broad edge on NSE 5-min after 0.10% cost.*
- **What worked: ORB + VWAP as a momentum confluence.** Among all combos, opening-range-breakout-while-above-rising-VWAP was the least-bad base (−₹76/trade), and a deep cross-tab found a genuinely positive sub-population. Stacking economically-grounded filters — **Mon/Wed** (avoid expiry chop), **price ≥ ₹500**, **VWAP slope 20–50 bps** (with-flow, not exhausted), **gap ≥ 0**, **wide OR ≥ 1%**, and a **hold-to-time exit** — turned it strongly positive.
- **In-app validation:** 250-day run #105 = **69 trades, +₹184/trade, 49% win, 8/12 months**. 500-day cross-regime run #106 = **144 trades, +₹196/trade, 49% win, 16/24 months (67%)**, +₹28,165 net. **Per-trade is consistent across windows (~₹185–196), confirming a real ~+0.37R edge.**
- **Harness-vs-engine reconciliation (key lesson, now in the playbook):** the offline harness reported ~+₹466/trade; the in-app truth is ~₹196. A trade-by-trade diff showed it was **NOT a logic bug** — on the 36 trades both agreed on, the engine delivered +₹376. The gap was (a) the harness measuring idealized R×₹500 vs the engine's real integer-share fills + cost, and (b) the harness's file-based window reaching an older, more-favorable 2025 period the in-app's clean 250-trading-day window didn't. **The harness finds the right config but over-states magnitude; always reconcile in-app before trusting a number.**
- **Temperament:** ~49% win, regime-dependent — strong in trending quarters (2025 Q2 +₹380/trade, 2026 Q1 +₹378), weak/negative in choppy ones (2024 Q4, 2026 Q2). A market-regime filter (e.g. only-long-when-Nifty-above-its-VWAP) is the natural next improvement.

The preset config (Mon/Wed, price≥500, slope 20–50, gap≥0, OR≥1%, hold-to-time) is the in-app-validated lock. Still **in-sample — paper-trade before risking capital.**

## Shared trading config (per-preset, overridable)

`MarketOpenTime 09:15 · MarketCloseTime 15:30 · EntryTime 09:30 · ExitTime 15:15 · FixedStopLoss ₹500 · FixedTarget ₹2000 · TargetMultiplier 2.5 · TrailStepMultiplier 2.0 · MaxTradesPerDay 2 · MaxCapitalPerTrade ₹3,00,000`

## Built-in lock + edits

- Built-in presets cannot be edited or deleted. The UI shows **Reset to defaults** (re-seeds the original values) and **Clone** (creates a user-editable copy).
- User presets support full CRUD.

## Adding a new strategy

See `extending.md`.
