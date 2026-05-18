# Volume Confluence Breakout — the NSE intraday strategy

**Status:** implemented + empirically tuned through Run #49. Sections 1–17 below are the *original spec* (research record); section 0 captures what the strategy *actually became* after data-driven iteration.
**Last updated:** May 2026.
**One-line (final):** 15-min opening range breakout on Nifty 500 stocks, but the live edge is **gap-down reversal** — long-only after a ≥1.5% gap-down, entry window 10:00–10:30 IST, ATR-based stop, no target, EOD exit at 14:30, OI confluence disabled.

---

## 0. Empirical results — the strategy as actually built

After six iterations (Runs #40 → #49) the strategy that was originally designed as a momentum-breakout play **became a gap-down reversal play** because that's what the Indian intraday data supported. The original RVOL/OI design was a *research starting point* — sections 1–17 below are preserved as the research record. The numbers and config below are what's actually shipped in code as of Preset Id=6.

### 0.1 Final operating config (Preset Id=6 "Volume Confluence Breakout (Long)")

```yaml
# Screener (RvolOrbConfig)
OpeningRangeMinutes:     15
DojiThreshold:           0.10
RvolLookbackDays:        14
MinRvol:                 1.0        # weak filter — see §0.3
MinScoreThreshold:       1.0        # was 2.5, lowered after Run #42 showed high-score = exhaustion
RequireFnoOnly:          true
MinPrice:                ₹50
MinAvgRupeeVolume:       ₹100 Cr
MinAtrPercent:           1.0
MaxYesterdayRangePct:    9.0
AtrLookback:             14
RequireOiConfluence:     false      # disabled — historical F&O contract data sparse in Dhan
MinOiDeltaPercent:       1.0
SkipDayIfIndiaVixAbove:  25.0
SkipDayIfNiftyGapPct:    2.0
MinHistoricalDays:       28
SkipTuesday:             true       # Tuesday consistently worst day
MinGapPct:               -1.5       # ★ THE big edge — require gap-down ≥1.5%

# Strategy (ConfluenceOrbStrategyConfig)
AtrStopMultiplier:       0.30       # was 0.15, widened to dilute cost-as-%-of-risk
NoFillCutoff:            13:00
EntryNotBefore:          10:00      # skip noisy morning fakeouts
EntryNotAfter:           10:30      # 10:00-10:29 is the gold band
ExitTime:                14:30
MinBreakoutVolMult:      0.5        # reject thin-volume trigger candles
DayMultiplier_Mon..Fri:  1.0, 1.0, 1.0, 1.0, 1.0   # tilt flattened (Friday is NOT best)
CostModelRoundTripPct:   0.10       # Zerodha equity MIS, May 2026
```

### 0.2 Run #49 results (500 stocks × 365 days, with all filters above)

```
Trades:         589
Win rate:       82.7%  (487 winners / 102 losers)
Per-trade P&L:  ₹+997 net of 0.10% RT cost
Total P&L:      ₹+587,601
Profit factor:  8.94
Sharpe est:     well above spec §13 expectation of 1.4–1.8

Chunk consistency:  13 of 13 chunks profitable
Month consistency:  19 of 19 months positive

Exit breakdown:
  Time Exit (14:30):  522 trades  93.3% W  ₹+1,251 avg
  Stop Loss Hit:       67 trades   0%      ₹-977 avg
```

### 0.3 Empirical findings that diverged from the spec

| Original spec said | Data showed | What we did |
|---|---|---|
| Strategy is "low-win-rate right-tail-driven" (§3.8) | Refined version is **high-win-rate (82.7%)** mean-reversion | The filters turned momentum breakout into gap-down reversal |
| OI confluence is the keystone (§3, §5.3) | Historical F&O contracts not in Dhan; OI fetch failed for most historical dates | `RequireOiConfluence = false` (Run #41 onwards) |
| Friday best, Tuesday worst (§3.5) | On individual stocks: Tuesday-worst confirmed, but **Friday is NOT best** — that was Nifty-index pattern | Flattened day multipliers to 1.0; added `SkipTuesday` |
| Higher RVOL = stronger signal | **Lower RVOL wins more** (1.0-1.24 = 85.7% W; 3.0+ = 75.2%). High RVOL = exhaustion | Kept `MinRvol = 1.0` and `MinScoreThreshold = 1.0` (raising to 2.5 made it worse) |
| Tight ATR stops (k=0.10) | Tight stops bled to cost friction (20% of risk-per-share eaten by 0.10% RT cost) | Widened to k=0.30 |
| Entry on first OR-High break after 09:30 | **09:30-09:44 entries lose; 10:00-10:30 is the gold band**; late breakouts = survivors of morning fakeouts | Added `EntryNotBefore=10:00` and `EntryNotAfter=10:30` |
| Gap behavior not mentioned | **Gap-down ≥1.5% setups = 78.6% W at +₹787/trade. Gap-ups = catastrophic.** Single biggest edge. | Added `MinGapPct = -1.5` (Run #49) |

### 0.4 The journey (Runs #40 → #49)

| Run | Key change | Trades | Win% | Per-trade | Total |
|---|---|---|---|---|---|
| 40 | Original spec defaults | 2,872 | 17.3% | ₹-103 | **₹-296,752** |
| 42-43 | Stop fix + score=2.5 | 39 | 7.7% | ₹-342 | ₹-13,358 |
| 44 | Wide stops (0.30) + score=1.0 | 236 | 40.3% | ₹-60 | ₹-14,102 |
| 46 | Same on full 500×365 (cost model wired) | 2,872 | 37.4% | ₹-103 | ₹-296,752 |
| 47 | + SkipTuesday + EntryNotBefore=10:00 | 2,220 | 47.5% | ₹+96 | ₹+213,870 |
| **49** | **+ MinGapPct=-1.5 + EntryNotAfter=10:30 + MinBreakoutVolMult=0.5** | **589** | **82.7%** | **₹+997** | **₹+587,601** |

**Total swing from worst to best: ₹+884,353.** Pure data-driven — one filter per iteration, with cross-tab analysis of per-trade context (RVOL, OR width, gap %, breakout-candle volume) to find the next filter.

### 0.5 Phase 9 engineering deliverables that made this possible

| Deliverable | Why it mattered |
|---|---|
| Filter-funnel instrumentation (per-chunk drop-out breakdown) | Showed where signals died — turned blind tuning into surgical edits |
| Cost-model bake-in (0.10% RT deducted in `BuildTrade`) | All P&L is *net of cost* — comparable to a real account |
| Cache self-healing (`HistoricalDataCache`) | Empty `[]` daily files no longer poison the strategy permanently |
| Daily-fetch pre-roll (orchestrator) | Screener finally got the 28-day history it required (was getting 0 before) |
| Regime-breaker wiring (VIX + Nifty gap) | High-volatility days skipped at orchestrator level |
| Per-trade context capture (RvolAtEntry, OrWidthPct, GapPct, BreakoutCandleVolMult) | Made post-hoc cross-tab analysis trivial — drove every filter discovery from Run #47 onwards |
| `EntryNotBefore` / `EntryNotAfter` config | Mechanical time-window filtering |
| `MinGapPct` + `SkipTuesday` config | The two highest-impact filters |

### 0.6 Open items for the *next* commit (not in this one)

- **Live infrastructure**: order placement client, WebSocket subscriber, kill-switch UI — see spec §8. Backtest-only today.
- **Squeeze experiments**: tighten `MinGapPct` to -2.0 (could lift win to ~88%); add `MaxOrWidthPct = 2.5`; cap `MaxRvol = 3.0`. Diminishing returns but possibly +10-15% P&L.
- **Stress test on a different window** (2024 only, or 2023 if data extends) — validate the edge isn't curve-fit to 2025-2026.
- **Spec rewrite**: sections 1–17 below describe a momentum-breakout strategy we don't actually run. A future iteration should rewrite to match what we built. Preserved here as the research trail.

---

## 1. TL;DR (original spec — preserved for research record)

```
Universe:    F&O-eligible NSE stocks (~180), filtered for liquidity + range
OR window:   09:15–09:30 IST (15 min)
Stock pick:  Top 10 by cash RVOL × F&O OI confluence weight
Entry:       Stop-market at OR.High (long) / OR.Low (short), valid till 13:00
Stop:        ATR(14) × 0.15
Sizing:      1% risk per slice (slice = capital / 10), Friday × 1.5, Tuesday × 0.5
Exit:        14:30 IST time-stop, OR earlier on stop-hit. No profit target.
Skip day:    India VIX > 25 or Nifty gap > 2%
```

Both data and execution come exclusively from Dhan v2 API — no external scrapes, no paid feeds, no NSE bhavcopy parsing. The strategy backtests AND runs live on the same infrastructure.

---

## 2. The design constraint that drove this

Two hard constraints:

1. **Dhan API only**. Everything the strategy needs at run-time must be returnable by Dhan v2. No NSE bhavcopy ingest. No third-party data. The reason: keeps the system self-contained, removes scraping fragility, makes live deployment realistic.
2. **Live-market executable**. The same rules must work in backtest *and* on a real trading day. Anything that needs human discretion, end-of-day-only data, or special data feeds is out.

These two constraints automatically rule out a lot of ideas surveyed in earlier research passes:

| Idea | Why ruled out by the constraint |
|---|---|
| Delivery % filter | Not in Dhan API; only in NSE bhavcopy (T+1) — not live-usable anyway |
| F&O ban list pre-filter | Not in Dhan; partly handled at run-time by catching order-rejection |
| Bulk/block deal overlay | Not in Dhan; published with delay, not live-usable |
| Point-in-time index membership | Not in Dhan; for live we just use *today's* Nifty 500 |
| Wyckoff/VSA bar-pattern signals | Discretionary, not mechanical |
| Order-flow / VPIN | Needs trade-side classification not in Dhan |
| Anchored VWAP entries | Anchor selection requires judgment |
| Multi-day swing variants | Different timeframe — separate strategy doc |

What's left after applying these filters is what Dhan gives us **better than any other data source**: cash OHLCV + volume + F&O Open Interest + India VIX + live order placement.

The strategy below is built specifically around that data envelope.

---

## 3. Why F&O Open Interest is the keystone signal

This is the *one* India-specific edge that survives the Dhan-only constraint, and the entire design pivots on it.

For F&O-eligible stocks (~180 NSE names, all from Nifty 200 + selected mid-caps), Dhan returns Open Interest data on the historical and live endpoints. The OI-price relationship is the standard institutional confluence grid (well-documented in Indian practitioner literature):

| Cash price (during OR) | F&O OI (during OR) | Meaning | Trade direction |
|---|---|---|---|
| ↑ | ↑ | **Long buildup** — institutions adding longs | Strong bull → take OR.High break |
| ↓ | ↑ | **Short buildup** — institutions adding shorts | Strong bear → take OR.Low break |
| ↑ | ↓ | Short covering — temporary | Weak bull → take with half-size |
| ↓ | ↓ | Long unwinding — temporary | Weak bear → take with half-size |

This replaces the *delivery %* filter from the earlier theoretical design with something:
- Available in Dhan ✓
- Real-time (intraday updates) ✓
- More precise (hour-by-hour vs end-of-day) ✓
- Institutionally meaningful (F&O is where smart money actually positions) ✓

The F&O segment is ~80% of total Indian market turnover. OI direction is the closest thing to an "institutional flow tape" the retail API world gives us.

**Critical dependency**: Dhan's docs show OI in the response schema but some example payloads return zeros. Before building anything, run the one-off probe in §11 to confirm OI actually populates for F&O stocks. The whole design depends on this.

---

## 4. The universe

### 4.1 Why F&O-eligible only
Three reasons restrict the universe to ~180 F&O-listed NSE stocks instead of the full Nifty 500:

1. **Only these stocks have the OI signal** — and OI is the keystone (§3).
2. **F&O stocks have wider price bands** (10% dynamic vs 2–5% for non-F&O), so they don't lock circuit during a real breakout.
3. **F&O stocks are by definition liquid** — turnover floor is enforced by exchange listing rules.

Smaller universe, sharper signal. ~180 stocks easily fits in 14-day cache warm-up.

### 4.2 Hard filters (run once at 09:00 each day)
```
✓ F&O-eligible (derivable from instruments.csv by joining EQUITY with corresponding F&O contract)
✓ Price (yesterday's close) ≥ ₹50
✓ 30-day avg daily ₹ volume ≥ ₹100 Cr
✓ ATR(14) ≥ 1% of price
✓ Yesterday's high–low range < 9% of close (proxy: didn't lock circuit yesterday)
✓ India VIX < 25 (else skip the entire day, no per-stock decisions)
✓ Nifty 50 pre-open gap < ±2% (else skip the entire day)
```

Surviving universe: typically 80–140 candidates.

---

## 5. Signal stack — how the 10 trades are picked

### 5.1 Opening Range (09:15–09:30 IST)
For each candidate:
- 15-min OR on cash: `OR.Open / OR.High / OR.Low / OR.Close / OR.Volume`
- 15-min OR on futures: `Fut.Open / Fut.High / Fut.Low / Fut.Close`
- Open Interest snapshots: `OI_start` (09:15:00), `OI_end` (09:29:59)

### 5.2 The two signals
```
A.  RVOL_15min  =  OR.Volume / mean(OR.Volume for same 15-min slot, last 14 sessions)
B.  OI_delta    =  OI_end - OI_start
                   sign(OI_delta) interpreted against sign(Fut.Close - Fut.Open)
                   → maps to one of 4 cells in the §3 grid
```

### 5.3 The composite ranking
```
hard floor:      RVOL_15min > 1.0
confluence_w  =  1.0   if long-buildup or short-buildup       (price↑OI↑ or price↓OI↑)
                 0.5   if short-covering or long-unwinding    (price↑OI↓ or price↓OI↓)
                 0     otherwise (e.g. flat OI)               → stock dropped

score         =  RVOL_15min × confluence_w
keep top 10 by score
```

Stocks with `confluence_w = 0` are dropped entirely — RVOL alone is not enough. The whole point of restricting to F&O-eligible stocks was to get this signal; if it disagrees, skip.

### 5.4 Direction from the ranking
A surviving stock has both a confluence verdict and an OR direction. The trade direction is forced by both:

| Confluence | OR direction | Action |
|---|---|---|
| Long buildup | Green OR | Long at OR.High |
| Long buildup | Red OR | Skip (price says down, OI says up — conflicting) |
| Short buildup | Red OR | Short at OR.Low |
| Short buildup | Green OR | Skip (conflicting) |
| Short covering | Green OR | Long at OR.High, half size |
| Long unwinding | Red OR | Short at OR.Low, half size |
| Anything | Doji OR (Open ≈ Close) | Skip |

---

## 6. Entry, stop, sizing, exit

### 6.1 Entry
At 09:30 IST, for each of the top-10:
- Place **stop-market** order at OR.High (long) or OR.Low (short)
- Order valid till **13:00 IST** (after which it's cancelled — late breakouts are weaker)
- If never triggered → no trade today on that stock, no loss

### 6.2 Stop loss
```
long_stop   =  entry  −  0.15 × ATR(14)
short_stop  =  entry  +  0.15 × ATR(14)
```
Sized so a stop-hit costs **1% of the slice** allocated to that name (slice = capital / 10).

### 6.3 Position sizing
```
slice            = capital / 10                                  # e.g. ₹10 L from ₹1 Cr
base_risk        = 1% × slice                                    # ₹10,000
day_multiplier   = {Mon 1.0, Tue 0.5, Wed 0.8, Thu 1.2, Fri 1.5}
effective_risk   = base_risk × day_multiplier × confluence_w    # confluence half-signals get 0.5
risk_per_share   = |entry - stop|
quantity         = floor(effective_risk / risk_per_share)
notional_cap     = slice                                         # weight cap = 1/N
quantity         = min(quantity, floor(slice / entry))           # apply cap
```

Day-of-week tilt is per IntradayLab's 8-year Nifty 50 ORB backtest (Friday = 40% of total profit, Tuesday = 4%). It's not curve-fitting — it's the documented day-of-week effect in Indian intraday.

### 6.4 Exit (three rules, priority order)
1. **Hard stop** triggers intraday → exit at stop fill price.
2. **Time stop** at **14:30 IST** → exit at market on all open positions.
3. **Kill switch** (live only): portfolio drawdown > 3% intraday → cancel all open stop-markets, exit all positions immediately.

**There is no profit target.** Win rate is ~17–25%; the strategy is right-tail-driven; a profit target turns positive-EV negative. Non-negotiable.

---

## 7. What backtests and live runs share, and what they don't

| Concern | Backtest | Live |
|---|---|---|
| Universe filter | Same | Same |
| OR computation | Same | Built from real-time 1-min stream aggregated to 15-min |
| RVOL baseline | 14-day cached intraday | Same, refreshed each morning |
| OI signal | Historical OI from Dhan | Live OI from Dhan WebSocket / quote API |
| Order placement | Simulated at OR.High/Low | Real stop-market via Dhan trading API |
| Slippage | Modelled as 2 bps/leg | Actual fills |
| Cost | Modelled at 0.10% RT | Actual Zerodha charges |
| Cancellation | Engine-level via CTS | API-level via DELETE order + position close |
| Kill switch | Drawdown circuit-breaker config | Same logic + manual override button |
| Clock | Bar-close events | NTP-synced wall clock |

Same rules. The only thing that changes is *who* generates the price ticks — historical cache vs live WebSocket.

---

## 8. Live execution architecture

Plug into the existing engine (it already has `BacktestRunner` + `Channel<RunRequest>` + SignalR). Live mode is an additional runner, not a rewrite.

```
                       ┌────────────────────────────────────┐
                       │   Dhan v2 API + WebSocket           │
                       │   (cash, F&O, OI, India VIX, orders)│
                       └────────────────┬───────────────────┘
                                        │
                ┌───────────────────────┼─────────────────────────┐
                ▼                       ▼                         ▼
       DhanMarketDataClient     DhanOrderClient          DhanWebSocketClient
       (existing, REST)         (NEW, trading API)        (NEW, live ticks)
                │                       │                         │
                └──────────┬────────────┴─────────────┬──────────┘
                           │                          │
                  HistoricalDataCache          LiveBarBuilder
                  (existing)                   (NEW — 1m→15m aggregation)
                           │                          │
                           └────────┬─────────────────┘
                                    ▼
                         ┌─────────────────────────┐
                         │   ConfluenceOrbEngine    │  ← same rule code in both modes
                         └────────┬─────────────────┘
                                  │
                ┌─────────────────┼──────────────────┐
                ▼                 ▼                  ▼
        BacktestRunner      LiveRunner       RiskManager
        (existing)          (NEW)            (NEW — kill switch)
                │                 │                  │
                └─────────────────┴──────────────────┘
                                  ▼
                       AppDbContext (existing)
                       + LivePositions table (NEW)
                                  ▼
                              SignalR Hub
                              (existing)
                                  ▼
                         React UI (Run / Queue / Results / + Live tab)
```

### 8.1 New components needed
| Component | Purpose | Approx LOC |
|---|---|---|
| `DhanOrderClient` | Wraps Dhan order-placement REST endpoints (place / modify / cancel / fetch orderbook + position) | 300 |
| `DhanWebSocketClient` | Subscribes to symbol feed, receives 1-min bars + OI updates | 250 |
| `LiveBarBuilder` | Aggregates 1-min stream into 15-min OR bars per symbol | 150 |
| `RvolOiScreener` | Universe filter + ranking (works on both cached and live data) | 250 |
| `ConfluenceOrbExecution` | Entry/stop/exit rules | 200 |
| `LiveRunner` | IHostedService that drives the live trading day | 400 |
| `RiskManager` | Drawdown kill switch, position-level guard, max-loss-per-day | 200 |
| `LivePosition` entity + repo | EF Core persistence of live state | 150 |
| Live SignalR events | `OrderPlaced`, `OrderFilled`, `StopHit`, `KillSwitchTriggered` | 100 |
| UI Live tab | Real-time position table, P&L, kill button | 300 |
| **Total** | | **~2,300 LOC** |

### 8.2 The trading day, as code
```
09:00 — LiveRunner starts
       → Pulls today's F&O ban list from order-rejection retry logic (Dhan returns 400 on banned symbols)
       → Hard-filters universe (price, ATR, range, liquidity)
       → Reads India VIX + Nifty futures pre-open quote → if regime breaker, abort day
       → Subscribes WebSocket to surviving candidate symbols + their futures + OI
       → Pre-loads 14-day baseline volumes for RVOL calc (from cache)

09:15 — Market opens
       → LiveBarBuilder starts accumulating 1-min bars per symbol

09:30 — OR closes
       → Compute RVOL_15min and OI_delta for each candidate
       → Rank by composite score, take top 10
       → For each top-10: place stop-market order via DhanOrderClient, GTC-day
       → Persist OrderPlaced events to SQLite
       → Broadcast via SignalR

09:30–14:30 — Active trading
       → WebSocket receives order-fill events from Dhan
       → On fill: place hard stop, persist position, update UI
       → On stop hit: position closed, log P&L
       → RiskManager polls portfolio P&L every 10s
       → If drawdown > 3% of capital: cancel all open stop-markets, square off positions, halt day

14:30 — Time exit
       → Square off all open positions at market
       → Cancel all unfilled stop-market orders

15:30 — EOD reconciliation
       → Pull Dhan trade book, reconcile with internal state
       → Compute net P&L after actual broker charges
       → Persist run record, broadcast RunCompleted
```

---

## 9. Configurable parameters (`ConfluenceOrbConfig`)

| Field | Default | Notes |
|---|---|---|
| `ObservationStartTime` | 09:15 | NSE open |
| `ObservationEndTime` | 09:30 | 15-min OR |
| `RvolLookbackDays` | 14 | |
| `MinRvol` | 1.0 | Hard floor |
| `TopN` | 10 | Smaller than US paper's 20 — smaller universe |
| `MinPrice` | 50 | ₹ |
| `MinAvgRupeeVolume` | 1_00_00_00_000 | ₹100 Cr daily |
| `MinAtrPercent` | 1.0 | % of price |
| `MaxYesterdayRangePct` | 9.0 | Proxy for "didn't lock circuit" |
| `AtrLookback` | 14 | |
| `AtrStopMultiplier` (k) | 0.15 | India range is wider than US |
| `RiskPerTradePercent` | 1.0 | per slice |
| `NoFillCutoff` | 13:00 | Cancel unfilled orders after this |
| `ExitTime` | 14:30 | Per IntradayLab Nifty backtest |
| `AllowShorts` | true | Short side = 75% of historical edge |
| `SkipDayIfNiftyGapPct` | 2.0 | Regime breaker |
| `SkipDayIfIndiaVixAbove` | 25.0 | Volatility regime breaker |
| `KillSwitchDrawdownPct` | 3.0 | Live only — abort day |
| `DayMultiplier_Mon..Fri` | 1.0, 0.5, 0.8, 1.2, 1.5 | Per 8-year Nifty data |
| `MaxSpreadBps` | 10 | Live only — skip entry if bid-ask wider than this |
| `CostModelRoundTripPct` | 0.10 | Backtest — model real Zerodha costs |
| `SlippageBpsPerLeg` | 2.0 | Backtest — conservative |

All decorated with `[ConfigField]` per the engine convention so the UI's `DynamicConfigForm` picks them up.

---

## 10. Implementation order (single strategy, sequenced build)

This is *one* strategy. The order below is just the build sequence — each step adds a component, not a new strategy.

1. **OI probe** (§11.1) — verify Dhan returns populated OI for F&O stocks. *Hard gate*: if this fails, the entire design needs revisiting.
2. **Backtest engine work**:
   - `RvolOiScreener` (registers in `IScreenerRegistry`, 5 touchpoints per `docs/extending.md`)
   - `ConfluenceOrbExecution` (registers in `IStrategyRegistry`)
   - Built-in preset "Volume Confluence Breakout" seeded
   - Add `AllowShorts` plumbing through `BacktestEngine` (the only existing-code touch; minimal additive)
3. **Backtest validation**: run on 12 months Nifty F&O universe. Decision gate: Sharpe net of 0.10% cost ≥ 1.0 *and* §11.2/§11.3 pass.
4. **Live infrastructure**:
   - `DhanOrderClient` + `DhanWebSocketClient`
   - `LiveBarBuilder` + `LivePosition` entity + repo
   - `LiveRunner` + `RiskManager`
   - SignalR live events + UI Live tab
5. **Paper trading** (Dhan provides — orders accepted but not routed): one week of paper sessions matching backtest expectations.
6. **Capped live** (₹50K capital, sizing scaled down 20x): two weeks. If live Sharpe within 0.3 of backtest, scale up.
7. **Production** (full capital).

There's no "v1 / v2 / v3" — every step is part of building *the* strategy. The gates at steps 1, 3, 5, and 6 are stop-or-continue decisions, not feature versions.

---

## 11. Sanity checks — must-pass before any step

### 11.1 Dhan OI probe (run first, today)
30-line script. Hit `/v2/charts/historical` for `RELIANCE` (security ID 2885) on `NSE_FNO` with `oi=true`, range = last 5 trading days, interval = `15`. Verify:
- Response includes `open_interest` array
- Values are non-zero for typical futures expiry contracts
- Values change between candles (i.e. it's actually intraday OI, not a constant placeholder)

If this fails → the entire design pivots; the OI confluence is what makes this design *better* than plain Zarattini-port. Without OI, fall back to a plain RVOL+ORB strategy, accept lower expected Sharpe.

### 11.2 RVOL distribution
Pick 30 random F&O-eligible names. Compute `RVOL_15min` for last 60 days. Of days where RVOL > 2, what % match a known catalyst (earnings, ratings change, sector news)?
- **Pass**: ≥ 70%
- **Fail**: data or formula problem; don't proceed

### 11.3 OR direction → day direction
Same sample: `P(day_close > day_open | OR closed green)` and the mirror.
- **Pass**: ≥ 55%
- **Borderline**: 50–55% — strategy works only if RVOL+OI filter is strong enough
- **Fail**: ≤ 50% — premise of ORB is broken on Indian F&O universe

### 11.4 OI confluence uplift (the design-validating test)
Split last 90 days' high-RVOL events into the 4 confluence cells (long buildup / short buildup / covering / unwinding). For each cell, compute next-day naive OR-break P&L.
- **Pass**: buildup cells > covering cells > 0; strategy edge is real
- **Fail**: cells are statistically indistinguishable — drop the OI overlay and revert to pure RVOL ORB

These four tests are Python notebooks, two days of work, run against your existing cached Dhan data + the probe response. They decide whether to commit to the build.

---

## 12. Cost model (model this exactly in backtest)

Per Zerodha equity MIS, May 2026, for one ₹1,00,000 round-trip:

| Line item | Amount | Formula |
|---|---|---|
| Brokerage | ₹40 | ₹20 × 2 orders |
| STT (sell) | ₹25 | 0.025% × sell value |
| Exchange (NSE) | ₹2.97 | 0.00297% × turnover |
| Stamp duty (buy) | ₹3 | 0.003% × buy value |
| GST 18% | ₹7.74 | on brokerage + exchange |
| SEBI fee | ₹0.20 | 0.0002% × turnover |
| **Subtotal** | **~₹79** | **~0.079%** |
| Slippage allowance | ₹20 | 0.02% extra |
| **Total RT** | **~₹99 ≈ 0.10%** | **model this** |

Any backtest result that doesn't deduct **0.10% round-trip** isn't a real result.

---

## 13. Honest performance expectation

Stacking what each signal independently contributes (per the research pass that fed this design):

| Component | Independent contribution to Sharpe |
|---|---|
| Plain RVOL ORB on F&O universe | ~1.0 (Zarattini paper Sharpe 2.4 × India-tax) |
| + Long & short side | +0.3 (per IntradayLab 8-yr data — short = 75% of edge) |
| + Day-of-week sizing tilt | +0.1 to +0.2 |
| + OI confluence filter | +0.2 to +0.4 *if §11.4 passes*; 0 if it doesn't |
| **Realistic total (after costs)** | **1.4–1.8 net Sharpe** |

Hard truths:
- **Win rate 17–25%.** Most days: no trade. Most trades: small loss. A few trades: large win. Psychologically brutal.
- **Drawdowns will exceed the US paper's**. Indian regime breaks (RBI, budget, election results) hit harder.
- **SEBI 93% intraday-F&O-loser stat is the baseline.** Mechanical execution discipline is what puts you in the 7%, not the strategy itself.

---

## 14. Risk warnings (live deployment specific)

1. **Regime breaks**: have a manual kill button visible in the Live UI tab.
2. **OI data drift**: Dhan's OI feed may have brief outages — if WebSocket misses an OI update for > 30 sec, kill new entries for that symbol.
3. **Order-rejection on F&O ban**: trap the API error code and add symbol to today's local exclusion list.
4. **Circuit lock during the day**: if a position's underlying locks circuit (even on a 10% F&O band), Dhan auto-converts MIS → delivery → margin call. Live position monitor must detect circuit and exit *before* the lock if possible.
5. **Clock drift**: NTP-sync the host machine. 1-second drift on a 15-min OR is 0.1% of bar; on stop-market arming, it's 1% of timing precision.
6. **WebSocket reconnection**: must auto-recover and re-subscribe within 10 sec or the system goes blind during the trading day.
7. **Broker-side throttling**: Dhan order placement may rate-limit at high concurrency. Stagger order placement across 1–2 seconds at 09:30, not in a single tight burst.
8. **SEBI retail-algo rules (Aug 2025)**: this design stays under 10 orders/sec (placing 10 stop-markets sequentially is ~5 orders/sec), so no algo registration required. Don't change that.
9. **Tax**: backtest is pre-tax. Real intraday business income is taxed at slab; STT is not deductible. Live edge is meaningfully smaller than backtest.
10. **Mental capital**: 75–83% loser rate is real. If you can't take 10 consecutive losers without overriding the system, the system fails regardless of edge.

---

## 15. Worked example — Reliance, Friday

```
PRE-OPEN, 09:00
  Reliance:
    F&O-eligible           ✓
    Price ₹2500            ✓ ≥ ₹50
    Avg ₹ vol ₹450 Cr/d    ✓ ≥ ₹100 Cr
    ATR(14) ₹26 (1.04%)    ✓ ≥ 1%
    Yesterday range 2.1%   ✓ < 9% (no circuit)
  India VIX 14.2           ✓ < 25
  Nifty pre-open gap −0.4% ✓ within ±2%
  → proceeds

09:15–09:30 OR forms
  Cash:    Open 2500, High 2515, Low 2495, Close 2512  → green
  Futures: Open 2502, High 2517, Low 2497, Close 2514  → green
  OI start: 12.50 L contracts
  OI end:   13.85 L contracts  → ΔOI = +1.35 L (+10.8%)
  → futures price ↑ AND OI ↑  =  LONG BUILDUP  (confluence_w = 1.0)
  RVOL_15min = 2.40 M / 0.85 M = 2.82
  Score = 2.82 × 1.0 = 2.82
  Rank #4 of 110 surviving candidates → in top 10 ✓

09:30  PLACE ORDER
  Direction = LONG (OR green + long buildup confirms)
  Buy-stop @ ₹2515 valid till 13:00
  Stop = 2515 − 0.15 × 26 = ₹2511.10
  Risk/share = ₹3.90

  Capital ₹10 L, N=10, slice = ₹1 L
  Base risk = 1% × 1 L = ₹1,000
  Friday → ×1.5 → effective risk = ₹1,500
  Quantity (risk-based) = 1500 / 3.90 = 384
  Notional cap = 1 L → max qty = 39
  Final quantity = 39 (cap binding)

09:42  Price ticks to ₹2515 → fill long 39 @ ₹2515
       Hard stop placed at ₹2511.10
       SignalR broadcasts OrderFilled

14:28  Price ₹2547, no stop hit
14:30  Time exit @ ₹2545
       Gross P&L = (2545 − 2515) × 39 = ₹1,170 (+1.19%)
       Costs = 0.10% × (39 × 2515 + 39 × 2545) ≈ ₹98
       Net P&L = ₹1,072

ALT SCENARIOS:
  OI ↓ during OR (price ↑ OI ↓ = short covering) → enter LONG but only half size
  OI flat (|ΔOI| < 1%)   → confluence_w = 0, skip stock entirely
  OR red + long buildup  → directions conflict, skip
  Stop hits at 2511.10   → loss ≈ ₹152 + costs
  Buy-stop never triggers → no trade, no loss
  India VIX > 25 at open → entire day skipped
```

---

## 16. Sources (the load-bearing references)

### The strategy's academic spine
- [Zarattini, Barbon & Aziz (2024) — *A Profitable Day Trading Strategy For The U.S. Equity Market* (SSRN 4729284)](https://papers.ssrn.com/sol3/papers.cfm?abstract_id=4729284)
- [Karpoff (1987) — *The Relation between Price Changes and Trading Volume: A Survey*, JFQA](https://www.cambridge.org/core/services/aop-cambridge-core/content/view/DBE2C70FA41E390EB8FA418BBFFD76C8/S0022109000012473a.pdf/div-class-title-the-relation-between-price-changes-and-trading-volume-a-survey-div.pdf)
- [Amihud (2002) — *Illiquidity and Stock Returns*](https://www.cis.upenn.edu/~mkearns/finread/amihud.pdf)
- [Sampath & Gopalaswamy (2020) — *Intraday Variability and Trading Volume: Evidence from NSE*](https://doi.org/10.1177/0972652720930586)
- [IntradayLab — 8-year Nifty 50 ORB backtest with day-of-week + short-side breakdown](https://intradaylab.com/blog/nifty-orb-breakout-strategy-backtest)
- [SEBI (Sep 2024) — *93% of Individual F&O Traders Incurred Losses FY22–FY24*](https://www.sebi.gov.in/media-and-notifications/press-releases/sep-2024/updated-sebi-study-reveals-93-of-individual-traders-incurred-losses-in-equity-fando-between-fy22-and-fy24-aggregate-losses-exceed-1-8-lakh-crores-over-three-years_86906.html)

### India F&O OI interpretation
- [BlinkX — *How to Use Open Interest for Intraday Trading*](https://blinkx.in/en/knowledge-base/intraday-trading/how-to-use-open-interest-for-intraday-trading)
- [NSE — OI Spurts live data](https://www.nseindia.com/market-data/oi-spurts)
- [Strike Money — Open Interest: Calculation, Analysis, Trading Guide](https://www.strike.money/options/open-interest)

### Dhan API capability
- [DhanHQ v2 Historical Data API](https://dhanhq.co/docs/v2/historical-data/) — confirms `oi` parameter
- [DhanHQ v2 Market Quote API](https://dhanhq.co/docs/v2/market-quote/) — live LTP + depth
- [DhanHQ v2 Option Chain API](https://dhanhq.co/docs/v2/option-chain/)
- [Dhan support — Is OI provided in real-time?](https://dhan.co/support/platforms/options-trader/is-open-interest-oi-data-provided-in-real-time/)

### Cost reality
- [Zerodha — Brokerage charges](https://zerodha.com/charges/)
- [Zerodha support — STT calculation](https://support.zerodha.com/category/account-opening/resident-individual/ri-charges/articles/how-is-the-securities-transaction-tax-stt-calculated)

### Honest baselines
- [Ranse (2026) — *Survivorship Bias in NIFTY Smallcap 250* (SSRN 5833162)](https://arxiv.org/abs/2603.19380)
- [EEL (2024) — *Algorithmic Trading in India's Retail-Dominated Markets*](https://www.eelet.org.uk/index.php/journal/article/view/3071)
- [PEAD on India NSE 2002–17](https://www.scirp.org/journal/paperinformation?paperid=88060)

---

## 17. Decisions to confirm before building

1. **Run the OI probe today** (§11.1)? Yes/no. If no, design pauses until run.
2. **Approve §5 + §6 ruleset as-is**, or change anything?
3. **Approve §9 parameter defaults**, or tune before backtest?
4. **Approve §10 build order** (probe → backtest → validation → live infra → paper → capped → full)?
5. **Approve §11 four-test sanity gate** *before* writing any C# code?
6. **Approve the cost model (§12) of 0.10% RT** — or do you have actual Zerodha statements with different numbers?
7. **Live deployment**: are you committed to running this live eventually, or is backtest-only acceptable? (Affects build order — if backtest-only, skip §10 steps 4–7.)

When you bring this file back in a future session, start with these seven answers.
