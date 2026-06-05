# Strategies

A "strategy" in this app is a `(screener, execution)` combo wrapped in a named preset. There are **4 built-in presets** seeded into SQLite on first run; users can clone or create their own.

> **History:** the four original experiments (Volume Spike, Dominance Breakout, Dominance Trailing, Opening Range Breakout) were removed in migration `RemoveLegacyPresets` (2026-06) along with their run history — they never developed a validated edge. The `breakout` screener class is retained (it had no preset) for ad-hoc custom presets.

The screener decides *which stocks qualify*. The execution strategy decides *how to enter/exit*.

## Built-in presets

| Preset | Screener | Execution | One-liner |
|---|---|---|---|
| **Gap Fade (Long)** | `gapfade` | `gapfadelong` | Quiet, ATR-normalized gap-downs on liquid trending stocks; confirmation-candle mean-reversion entry (see `1_strategy-rvol-orb.md` family) |
| **Volume Confluence Breakout (Long)** | `rvolorb` | `confluenceorblong` | F&O 15-min ORB filtered by cash RVOL + futures OI direction (see `1_strategy-rvol-orb.md`, `2_strategy-rvol-orb-integration.md`) |
| **EMA Gap-Down Reclaim (Long)** | `emapullback` | `emapullback` | Buy-the-dip: uptrending stock (2–10% above 20-day SMA) that gapped down ≥1.5%, entered on the intraday 9-EMA reclaim |
| **VWAP ORB Momentum (Long)** | `vwaporb` | `vwaporb` | Momentum: Mon/Wed liquid (≥30L/day), higher-priced (≥₹500) stock breaks above its 30-min opening-range high while holding a rising VWAP (slope 20–50 bps) on a non-negative gap day; held to 15:00. |

The EMA Gap-Down Reclaim and VWAP ORB Momentum presets were tuned empirically in-app (see their sections below); Gap Fade and Volume Confluence carry research-grade defaults.

## Screeners

### Breakout (`breakout`, `BreakoutConfig`)
- Close ≥ historicalLow + range × `BreakoutThreshold`
- Green candle with volume ≥ `VolumeMultiplier` × average

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

### Gap Fade & Volume Confluence
See `1_strategy-rvol-orb.md` and `2_strategy-rvol-orb-integration.md`.

## Execution strategies

### EMA Gap-Down Reclaim (`emapullback`, `EmaPullbackStrategyConfig`)
- Entry at the **open of the candle after** the screener's reclaim candle
- Stop = reclaim candle's **low**; `Quantity = floor(FixedStopLoss / riskPerShare)`
- Target = entry + `RiskRewardRatio` × risk (default **1.5R**)
- Hard time-stop at `HardExitTime` (15:00 IST)
- Optional `UseTrailingStop` (chandelier: arm at `TrailActivateR`, trail `TrailGapR` below the high) — tested and *worse* than the fixed 1.5R on 5-min, so default **off**
- P&L is net of `CostModelRoundTripPct` (0.10% round-trip)

**Tuning history (in-sample, 500 NSE stocks × 250 days, 5-min):** the raw "EMA pullback on all stocks" lost (−95k, PF 0.5) — cost ate the thin edge. Adding the daily-trend band, a min-stop floor, and tighter time windows reached breakeven; the **gap-down ≤ −1.5% selection** was the breakthrough (net **+66k, PF ~1.8, 58% win, all 13 months positive, max drawdown 7% of profit**). This is the locked-in default. Still **in-sample — paper-trade before risking capital**; gap-down opens can slip more than the modeled 0.10%.

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
