# RVOL+ORB+OI — Integration plan

**Companion doc to:** `docs/1_strategy-rvol-orb.md` (the strategy specification).
This file answers: *given our specific stack and the live Dhan v2 API, what does it take to ship this strategy?*

**Status:** verification gate §11.1 from the spec has **passed**. Two prerequisite bug fixes have already shipped to `master`. The strategy is buildable as designed.

---

## 1. TL;DR

```
Gate §11.1 (OI populated and varies intraday) ........... PASS
Dhan Data API subscription active until 26 May 2026 .... CONFIRMED
DhanDataApiClient intraday datetime format bug ......... FIXED (commit 495e772)
instruments.csv auto-refresh from Dhan scrip-master .... SHIPPED (commit 1231a7f)
Strategy fully buildable as designed ................... YES

Estimated work remaining (long-only v1): ~1,250 LOC over 14 modified + 5 new files
Trade.Direction migration (for shorts):  deferred to v2
Live trading (§8 of spec):               separate project, ~2,300 LOC, not in this plan
```

---

## 2. What the live probe proved

Run on **2026-05-16** against **RELIANCE-May2026-FUT** (security ID 66355, NSE_FNO), 15-min candles, 2026-05-13 09:15 IST through 2026-05-15 15:30 IST:

| Metric | Value |
|---|---|
| HTTP response | 200 OK in 0.10 s |
| Candle count | 74 (3 full trading days × ~25 bars) |
| OI field in response schema | yes (`open_interest` array, parallel to OHLCV) |
| Non-zero OI candles | 74 / 74 (100%) |
| Unique OI values | 70 / 74 |
| OI range over window | 80,155,500 → 84,474,000 contracts |
| First 5 OI values | 80,423,000 / 80,473,500 / 80,459,500 / 80,592,000 / 80,667,500 |

**Interpretation:** OI is real, populated, and varies bar-to-bar (not a daily snapshot). The §3 keystone confluence signal the strategy depends on is genuinely accessible via Dhan. The no-OI fallback plan from earlier drafts is **no longer needed**.

Probe script: `D:\Code\tools\dhan-api-probes\Probe-OpenInterest.ps1` (shared across projects, not part of this repo).

---

## 3. Verified Dhan v2 API capabilities

These were proven against the live API during the probe runs. Use this table as ground truth when wiring code — don't trust older comments in the codebase that disagree.

| Capability | Endpoint | Verified field shape |
|---|---|---|
| Intraday candles (EQ) | `POST /v2/charts/intraday` | `instrument:"EQUITY"`, `interval:"1\|5\|15\|25\|60"`, `oi:false`, `fromDate`/`toDate` as **`yyyy-MM-dd HH:mm:ss`**. No `expiryCode`. |
| Intraday candles (FUT + OI) | `POST /v2/charts/intraday` | `instrument:"FUTSTK"`, segment `NSE_FNO`, `oi:true`. Response includes `open_interest` array parallel to OHLCV. |
| Daily candles (any) | `POST /v2/charts/historical` | `instrument:"EQUITY"\|"FUTSTK"\|…`, **`expiryCode:0`**, `fromDate`/`toDate` as `yyyy-MM-dd` (date only). |
| LTP (single price) | `POST /v2/marketfeed/ltp` | Body: `{"NSE_EQ":[secId, …]}`. Returns `data.NSE_EQ.<secId>.last_price`. |
| Scrip master (instruments) | `GET https://images.dhan.co/api-data/api-scrip-master.csv` | Public, no auth. 16-column CSV. Already auto-refreshed daily by `ScripMasterDownloader`. |

**Findings worth noting:**

1. **Expired F&O contracts return 0 candles on intraday endpoint**, even within the 90-day intraday window. The data is gone for retrieval purposes once the contract expires. Backtests must therefore use the **active contract for each date** (resolved via `expiryDate` in the scrip master) rather than a single "RELIANCE FUT" identifier.
2. **DH-905 is overloaded.** Same error code fires for genuine bad-payload AND for some auth-adjacent edge cases. Don't treat DH-905 as definitive about *which* field is wrong.
3. **Trading API and Data API are separate subscriptions.** Same client-id + access-token authenticates both, but tokens minted before the Data API plan was activated may need regeneration to carry entitlement claims. Symptom: DH-902 on data calls while trading calls succeed.
4. **PowerShell 5.1 + `Invoke-RestMethod`** hangs ~100 s against `api.dhan.co` due to TLS 1.2 negotiation default. Use `curl.exe` from PS scripts, or set `[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12` explicitly.

---

## 4. What's already in place

### Already shipped to master

| Commit | What | Why it matters here |
|---|---|---|
| `495e772` | `fix(dhan-api): correct intraday datetime format + add daily range fetch` | Without this, every intraday fetch returns DH-905 — strategy is dead in the water. Also adds `GetDailyHistoricalAsync` from earlier GapFade Phase 0a work. |
| `1231a7f` | `feat: auto-refresh instruments.csv from Dhan scrip-master` | Without this, the F&O contract resolver can't find current-month contracts after instruments.csv goes stale (~weeks). Now self-healing per 24h cycle. |

### Already in place from earlier GapFade work (uncommitted on master but in working tree)

| Component | File | What it gives this strategy |
|---|---|---|
| Daily-candle range fetch + cache | `Infrastructure/Caching/HistoricalDataCache.LoadOrFetchDailyRangeAsync` | One API call per stock for entire pre-roll window (vs one-per-day). Reuse for ATR(14) baseline. |
| `ScreenerContext` (intraday + daily bundle) | `Core/Interfaces/IScreener.cs` | Screener already receives daily candles when `RequiresDailyCandles = true`. Reuse as-is. |
| ATR(14) Wilder helper | `Backtesting/Screeners/GapFadeScreener.ComputeAtrWilder` | Lift to a shared helper or copy into `RvolOrbScreener`. |
| `RequiredHistoricalDays` pre-roll knob | `Core/Interfaces/IScreener.cs` + `BacktestOrchestrator.cs:69` | Set high enough (28+) so RVOL baseline + ATR have data. |
| Holiday calendar extended through 2027 | `Infrastructure/Calendar/TradingCalendarService.cs` | Avoids "weekday is a trading day" bugs near holidays. |

**Important:** the GapFade Phase 0/1 work and a number of UI fixes still sit uncommitted on master. They don't block starting this strategy's implementation, but committing them in a clean Phase 8 / Phase 9 series first would make the next round of work easier to review.

---

## 5. What still needs to be built — file-by-file plan

Long-only v1 — defers the `Trade.Direction` migration. Trades only the 3 long-side cells from spec §5.4 (long buildup × green OR, short covering × green OR with half size). Skips short rows and conflicts.

### New files (5)

| File | Purpose | Approx LOC |
|---|---|---|
| `Infrastructure/Instruments/FuturesContractResolver.cs` | Given `(equitySymbol, asOfDate)`, returns the active month's FUTSTK security ID + expiry. Reads from `InstrumentService._instruments` already in memory. | 80 |
| `Infrastructure/Quotes/RegimeBreakerService.cs` | `IsDayTradeable(date) → bool`. Fetches India VIX (sec 21, `IDX_I`) and Nifty 50 (sec 13, `IDX_I`) for pre-open / open snapshots, compares against thresholds. | 100 |
| `Backtesting/Screeners/RvolOrbScreener.cs` | Universe filter + OR + RVOL + OI confluence + composite score, returns top-N candidate's signal candle. | 280 |
| `Backtesting/Strategies/ConfluenceOrbLongStrategy.cs` | Stop-market arm-and-fill at OR.High, ATR stop, no target, 14:30 IST time exit. | 220 |
| `Persistence/Migrations/<ts>_SeedRvolOrbPreset.cs` (auto-generated) | `InsertData` row 6 in `StrategyPresets`. | auto |

### Files to modify (14)

| File | Change | LOC |
|---|---|---|
| `Core/Models/Candle.cs` | Add nullable `decimal? OpenInterest` field. | 1 |
| `Infrastructure/Api/DhanHistoricalResponse.cs` | Add `open_interest` array; map to `Candle.OpenInterest` in `ToCandles()`. | 10 |
| `Infrastructure/Api/DhanDataApiClient.cs` | Add `oi` flag param to `GetIntradayCandlesAsync` (default false). Add `GetIndexIntradayAsync` overload (`instrument:"INDEX"`, `IDX_I`). | 60 |
| `Infrastructure/Caching/HistoricalDataCache.cs` | New `LoadOrFetchFutWithOiAsync` + `LoadOrFetchIndexAsync`. Use separate cache namespaces `NSE_FNO_OI/` and `IDX_I/` so existing OI-less JSON isn't conflated. | 120 |
| `Infrastructure/Instruments/InstrumentService.cs` | New `GetFnoEligibleEquities(int limit)`. Already auto-refreshes via `ScripMasterDownloader`. | 30 |
| `Core/Configs/ScreenerConfigs.cs` | Append `RvolOrbConfig` class with 16 `[ConfigField]`-decorated properties (per spec §9). | 110 |
| `Core/Configs/StrategyConfigs.cs` | Append `ConfluenceOrbStrategyConfig` with 8 fields (ATR mult, no-fill cutoff, exit time, 5 day-of-week multipliers). | 60 |
| `Backtesting/Screeners/ScreenerFactory.cs` | Add `"rvolorb"` switch case + entry in `GetAvailableScreeners`. | 6 |
| `Backtesting/Strategies/StrategyFactory.cs` | Add `"confluenceorblong"` switch case + entry in `GetAvailableStrategies`. | 6 |
| `Backtesting/Registry/ScreenerRegistry.cs` | Add `BuildEntry("rvolorb", …)` row. | 4 |
| `Backtesting/Registry/StrategyRegistry.cs` | Add `confluenceorblong` entry referencing `ConfluenceOrbStrategyConfig`. | 8 |
| `Api/Services/PresetExecutor.cs` | Add `"rvolorb" → "RvolOrb"` to screener-section switch (line ~82) AND `"confluenceorblong" → "ConfluenceOrbLong"` to strategy-section switch (line ~100). **This is the most-missed touchpoint** per CLAUDE.md. | 4 |
| `Backtesting/Engine/BacktestEngine.cs` + `BacktestOrchestrator.cs` | Add `RequiresFuturesCandles` flag on `IScreener`. When set, orchestrator also fetches FUT candles per stock per day via `LoadOrFetchFutWithOiAsync` and passes them through `ScreenerContext.Futures`. | 90 |
| `Persistence/Seeding/BuiltInPresets.cs` | Add `Id = 6` "Volume Confluence Breakout (Long)" preset with `MaxTradesPerDay = 10` override (default 2 would cap us at trade #2/day). | 50 |

### Engine touch: `ScreenerSignal` (cross-cutting, ~40 LOC)

To carry the **sizing multiplier** (1.0 for buildup, 0.5 for covering) from screener to strategy without abusing the signal-candles list, introduce:

```csharp
// Core/Interfaces/IScreener.cs
public sealed record ScreenerSignal(
    List<Candle> Candles,
    decimal SizingMultiplier = 1.0m);

public interface IScreener {
    // … existing methods …

    // New overload, default-implemented to wrap legacy result.
    bool MeetsSignal(ScreenerContext ctx, out ScreenerSignal signal) {
        var ok = MeetsConditions(ctx, out var candles);
        signal = new ScreenerSignal(candles ?? new());
        return ok;
    }
}
```

Then `BacktestEngine.BacktestDay` calls `MeetsSignal` and passes `signal.SizingMultiplier` to a new `IStrategy` overload. Existing screeners/strategies inherit default 1.0× and behave identically. This is the cleanest path; the "encode the multiplier as candle count" hack from earlier drafts is rejected.

---

## 6. Cross-cutting decisions

### D1. Where confluence math lives

Screener computes confluence_w (1.0 / 0.5 / skip). Returned via `ScreenerSignal.SizingMultiplier`. Strategy reads it and scales the calculated quantity.

### D2. Futures candle cache layout

```
data/
  NSE_EQ/{tf}/{secId}/{date}.json          ← unchanged
  NSE_FNO_OI/{tf}/{secId}/{date}.json      ← NEW, includes OI
  IDX_I/{tf}/{secId}/{date}.json           ← NEW, VIX + Nifty
```

Separate `NSE_FNO_OI` namespace because existing JSON files don't include `OpenInterest` — mixing them in the same folder breaks deserialization. Same reason for `IDX_I`.

### D3. Cross-stock top-N ranking

Spec §5.3 calls for ranking *top-10 across stocks*. Current engine is per-(stock, day); cross-stock ranking requires a two-pass orchestrator change. **v1 punt:** use a hard `MinScoreThreshold` (default 1.5) instead, no orchestrator change. Tune in backtest. Document deviation. Add ranking in v2 if results justify it.

### D4. ATR stop without target

`ConfluenceOrbLongStrategy` mirrors `OpeningRangeBreakoutStrategy.cs:106` for entry/quantity math but **omits the target check** in the monitoring loop. Only SL hit and 14:30 time exit close positions. Per spec §6.4: "There is no profit target."

### D5. Per-day skip via regime breaker

`RegimeBreakerService.IsDayTradeable(date)` is the first filter in `RvolOrbScreener` — cheapest gate. If `false`, screener returns empty for every stock on that day. No orchestrator-level "skip day" plumbing needed.

### D6. F&O contract resolution per date

For each `(equitySymbol, backtestDate)`, the resolver returns the **near-month** FUTSTK security ID — the contract whose expiry is the earliest one `>= backtestDate`. Avoids using a contract that was already expired on the backtest date.

---

## 7. Sequenced build order

Each phase ends with a verification gate that decides whether to proceed.

### Phase A — OI plumbing (foundational, ~150 LOC)

Touchpoints: `Candle.cs`, `DhanHistoricalResponse.cs`, `DhanDataApiClient.cs`, `HistoricalDataCache.cs`.

- Add `OpenInterest` field
- Wire `oi` parameter through API client
- Add `LoadOrFetchFutWithOiAsync` with new cache namespace
- **Gate:** unit-test fetch a known F&O contract for a recent window, assert OI populated. Same shape as `Probe-OpenInterest.ps1` but in-process.

### Phase B — F&O universe + index data + regime breaker (~210 LOC)

Touchpoints: `InstrumentService.cs`, new `FuturesContractResolver.cs`, new `RegimeBreakerService.cs`, `DhanDataApiClient.cs` (`GetIndexIntradayAsync`), `HistoricalDataCache.cs` (`LoadOrFetchIndexAsync`).

- New universe filter (~180 F&O-eligible names)
- Futures resolver per date
- VIX + Nifty50 fetch via `IDX_I`
- **Gate:** smoke-test `IsDayTradeable(2026-05-15)` returns true; resolver returns 66355 for `("RELIANCE", 2026-05-15)`.

### Phase C — `ScreenerSignal` engine touch (~40 LOC)

Touchpoints: `Core/Interfaces/IScreener.cs`, `Core/Interfaces/IStrategy.cs`, `Backtesting/Engine/BacktestEngine.cs`.

- Add `ScreenerSignal` record + `MeetsSignal` overload (default-implemented)
- Add `IStrategy` overload that receives the multiplier
- **Gate:** existing presets (GapFade, Opening Range Breakout, etc.) produce byte-identical backtest results. This is the load-bearing regression check.

### Phase D — Configs + screener + strategy classes (~670 LOC)

Touchpoints: `ScreenerConfigs.cs`, `StrategyConfigs.cs`, new `RvolOrbScreener.cs`, new `ConfluenceOrbLongStrategy.cs`.

- All 16 + 8 `[ConfigField]`-decorated properties
- Full screener filter chain (11 steps cheapest → most expensive)
- Strategy with stop-market scan-forward + ATR stop + 14:30 exit
- **Gate:** `dotnet build` clean (0 warnings).

### Phase E — Wiring (~28 LOC)

Touchpoints: `ScreenerFactory.cs`, `StrategyFactory.cs`, `ScreenerRegistry.cs`, `StrategyRegistry.cs`, `PresetExecutor.cs`.

Don't skip `PresetExecutor.BuildConfiguration` — it's the 5th touchpoint that most easily gets missed and breaks the UI silently.

- **Gate:** `GET /api/registry/screeners` returns the new `rvolorb` entry with all 16 fields. React Configs page renders them.

### Phase F — Built-in preset + EF migration (~50 LOC + auto)

Touchpoints: `BuiltInPresets.cs`, new EF migration via `dotnet ef migrations add SeedRvolOrbPreset`.

The preset's `TradingConfigJson` must override `MaxTradesPerDay` to at least 10 (default 2 truncates after the 2nd trade). Also: `MaxCapitalPerTrade` doubles as the per-slice cap — set to `capital / 10`.

- **Gate:** UI Run page shows "Volume Confluence Breakout (Long)" as a 6th built-in preset; can be selected and started.

### Phase G — Verification against live API (~no code, all measurement)

Per spec §11.2 – §11.4 (RVOL distribution, OR direction predictiveness, OI confluence uplift). These are statistical sanity checks against cached historical data.

- **Gate:** all three checks pass per spec thresholds. If §11.4 fails (OI overlay shows no uplift over plain RVOL+ORB), revert §3 confluence weighting and ship as plain RVOL+ORB strategy.

**Total: ~1,150 LOC over ~3-4 working days.**

---

## 8. Open questions and deferred items

### Deferred to v2 (not blocking long-only v1)

1. **Short side.** Requires `Trade.Direction` enum + EF migration + sign-aware PnL across all 6 existing strategies + CSV column + UI rendering. Per spec §13, shorts are 75% of historical edge — losing them is meaningful but recoverable later.
2. **True cross-stock top-N ranking.** v1 uses `MinScoreThreshold`. Add ranking in v2 if results show too many trades on borderline scores.
3. **Cost model.** No strategy in the project currently models STT / brokerage / slippage. Backtest PnL is gross-of-cost. Per spec §12 this is non-negotiable for live trading but acceptable for v1 backtest validation. Track separately.
4. **Live trading.** Spec §8 lists ~2,300 LOC of new components (`DhanOrderClient`, `DhanWebSocketClient`, `LiveBarBuilder`, `LiveRunner`, `RiskManager`, `LivePosition` entity, UI Live tab). Entirely out of scope here. CLAUDE.md notes the app is single-user localhost; pointing it at a broker requires a security review first.

### Known caveats (pre-existing, project-wide)

1. **Survivorship bias.** `Nifty500Stocks.cs` is a current snapshot. Multi-year backtests overweight survivors. Doesn't affect this strategy uniquely.
2. **Negative cache lost on restart.** Delisted-stock 404s re-attempt on every cold start.
3. **`MaxTradesPerDay` interaction.** Strategy assumes up to 10 concurrent positions. The preset MUST override the default of 2.
4. **`MaxCapitalPerTrade`.** Repurposed as the per-slice cap. Preset must set explicitly.

### Things to confirm before Phase A starts

1. Run the §11.1 probe once more on a fresh date to confirm OI behavior is stable. (Probe script in `D:\Code\tools\dhan-api-probes\`.)
2. Verify Phase C regression check is feasible: do we have at least one saved preset with a known backtest result to diff against?
3. Decide whether to commit GapFade Phase 0/1 work and UI fixes (currently uncommitted on master) before starting this build, or interleave.

---

## 9. Sources used in this integration plan

- `docs/1_strategy-rvol-orb.md` — original strategy specification
- Live probes against `https://api.dhan.co/v2/` (May 2026)
- Dhan v2 docs: <https://dhanhq.co/docs/v2/historical-data/>, <https://dhanhq.co/docs/v2/annexure/>, <https://dhanhq.co/docs/v2/market-quote/>
- `CLAUDE.md` — project orientation (5-touchpoint extension rule, behavior-preservation rule, working-directory anchor)
- `docs/extending.md` — adding a new screener/strategy
- `docs/architecture.md` — runtime flow
- Commits `495e772` (intraday datetime fix) and `1231a7f` (auto-refresh)

---

## 10. One-page status card

```
STRATEGY: Volume Confluence Breakout (RVOL × ORB × F&O OI confluence)
SPEC:     docs/1_strategy-rvol-orb.md
PLAN:     docs/2_strategy-rvol-orb-integration.md  (this file)

PRE-WORK STATUS
  ✓ Dhan Data API subscription active
  ✓ OI verified populated and varies intraday
  ✓ Intraday datetime format bug fixed (495e772)
  ✓ instruments.csv auto-refresh shipped (1231a7f)

BUILD STATUS (long-only v1)
  □ Phase A — OI plumbing (~150 LOC)
  □ Phase B — F&O universe + regime breaker (~210 LOC)
  □ Phase C — ScreenerSignal engine touch (~40 LOC)
  □ Phase D — Configs + screener + strategy (~670 LOC)
  □ Phase E — Factories + registries + PresetExecutor (~28 LOC)
  □ Phase F — Built-in preset + EF migration (~50 LOC)
  □ Phase G — §11.2–11.4 verification (statistical, no code)

DEFERRED
  - Short side (Trade.Direction migration)
  - Cross-stock top-N ranking (using threshold in v1)
  - Cost model (project-wide gap)
  - Live trading (separate ~2,300 LOC subsystem)
```
