# Strategy Optimization Playbook

**Purpose.** A reusable, strategy-agnostic method for taking *any* trading
strategy — a fresh idea or a losing backtest — to a **robust, positive-expectancy**
preset, and for an AI agent (Claude) to do it semi-autonomously.

**How to use it.** Hand this file to Claude together with:
1. a **strategy hypothesis** (the entry/exit idea, in plain words), and
2. your **success criteria** (below),

and say *"follow the playbook."* Claude then runs the loop, reports against your
criteria at each step, and only "locks in" a version that passes the gates.

The method is engine-agnostic — it assumes only that you can (a) run a backtest
over a universe × window, (b) get per-trade records into a queryable store, and
(c) tag each trade with feature values. The mechanical section maps this to *this*
repo's tooling.

---

## 1. Inputs you provide (these steer everything)

Fill these in per strategy; they define what "good" means and bound the search:

- **Hypothesis** — the core entry/exit logic and *why* it should have an edge.
- **Universe & window** — instruments and the backtest date range (bigger = more
  robust, but watch in-sample/out-of-sample split).
- **Constraints** — e.g. max trades/day, long-only vs both sides, capital per
  trade, instruments allowed.
- **Cost model** — realistic round-trip cost % (brokerage + taxes + slippage).
  *Never optimize against zero cost.*
- **Targets** — what return / per-trade expectancy / profit factor you want.
- **Risk tolerance** — acceptable max drawdown, losing streak you can sit through.
- **Hard rules** — anything non-negotiable (e.g. "don't eliminate net-positive
  trades", "must be explainable", "no overnight holds").

Everything below is judged *against these inputs*, not against generic ideals.

---

## 2. The one principle that matters most

> **Measure, don't guess. Let the data choose the thresholds.**

Thresholds picked from convention or intuition ("require strong trend", "need high
volume", "this gap size") are frequently **wrong or even backwards**. The reliable
way to set a filter is to **record the candidate feature on every trade, then
cross-tab winners vs losers by that feature** and read the threshold off the data.
A strategy may even change *identity* once the data speaks — let it.

---

## 3. The iteration loop

Change **one lever at a time** (or one clean set), re-run, measure, decide.

1. **Baseline.** Run the strategy loosely (few/no discretionary filters) over a
   large universe × long window. Get every trade into the DB.
2. **Split GROSS vs NET.** Compute PnL both with and without cost. This separates
   **edge** (gross) from **friction** (cost). Positive-gross / negative-net means
   a *cost or sizing* problem — don't "fix" the entry logic in that case.
3. **Instrument.** Record a set of candidate features on every trade (filters
   *off*, recording *on*). Generic feature menu:
   - timing: time-of-day, day-of-week, month, hold time
   - trend/context: distance from a higher-timeframe MA, regime indicator (ADX,
     etc.), relative strength vs index
   - event: gap %, prior-day range, news/volatility proxy
   - activity: relative volume, volume expansion on the trigger
   - structure: stop-distance %, entry-price bucket, ATR %
   - any feature your hypothesis claims matters
4. **Cross-tab.** For each feature, bucket it and show `n / win% / gross-PnL / avg`.
   Use **GROSS** so cost noise doesn't hide the entry edge.
5. **Identify the clean signal.** A keep-worthy filter's buckets are:
   - **monotonic or cleanly separated** (not one lucky middle bucket),
   - **economically explainable** (you can say *why*),
   - **large-sample** (good buckets hold a meaningful share of trades),
   - and cutting the bad buckets **doesn't discard net-positive trades**.
6. **Apply** the filter at the data-chosen threshold, re-run, measure.
7. **Robustness-gate** (section 5). Pass → keep; fail → revert.
8. Repeat until new filters only shave volume without improving risk-adjusted return.

Keep an iteration log (template at the end): what changed, the result, the lesson.

---

## 4. What makes a filter worth keeping vs. dropping

- **Keep:** economically-motivated, monotonic, large-sample, removes net-negative
  buckets only.
- **Drop/avoid:** a lone good middle bucket; a numeric band that's good *only*
  because it was chosen after seeing the data (selection bias → regresses live);
  anything that cuts trade count to an unrepresentative sample.
- **A high-quality but small-sample variant** is a *paper-test candidate*, not a
  lock-in. Expect its eye-popping numbers to regress.

---

## 5. Robustness gates — define "good" (tie thresholds to the user's criteria)

Headline PnL is never enough. A version is "good" only if it clears the gates
(defaults shown; tighten/loosen per the user's risk tolerance):

| Gate | Healthy default | Why |
|---|---|---|
| Max drawdown | < ~15% of net profit | survivability |
| Return / max-DD | > ~3 | risk-adjusted quality |
| Period consistency | most/all months (or weeks) positive | not a few lucky runs |
| Profit concentration | top-10 trades < ~25% of net; many distinct instruments | not one fluke |
| Longest losing streak | small enough to sit through psychologically | real-world tradeability |
| Sample size | enough trades that win% is meaningful (rule of thumb > ~150) | statistical confidence |
| Profit factor | > ~1.3 net to trade; > 1.5 is good | edge clears cost |

Always **report the gates, not just the PnL.** A +PnL backtest that fails them is
fragile/curve-fit.

---

## 6. Overfitting discipline

- Prefer **economically-motivated cuts** ("X loses because Y") over numeric
  fitting ("value between A and B is best").
- **Don't stack many ANDed filters** — each shrinks the sample; too many can drive
  trades to near-zero and the result becomes noise.
- **In-sample ≠ out-of-sample.** Everything tuned on one window is in-sample. The
  only true validation is **forward / paper trading**. Always say so.
- Honor the user's hard rules (e.g. "don't eliminate net-positive trades").

---

## 7. The cost reality (why short-timeframe strategies struggle)

> **cost-per-trade (in R) = round-trip-cost % ÷ stop-distance %**

If cost is a large fraction of per-trade risk, even a positive *gross* edge nets to
~0. Levers that beat it:
- **Better selection** — trade only setups whose expected move dwarfs the cost.
  (Usually the highest-leverage fix.)
- **Wider stops** — lower cost/R, but the target moves out too; test, don't assume.
- **Position size does NOT help** — it scales gross and cost equally.

---

## 8. Mechanical setup (this repo)

### 8.0 Iterate on a FAST offline harness, validate in-app ONCE at the end

> **The single biggest speed-up.** Don't iterate with the full in-app engine — it
> takes ~20 min/run (DB writes, SignalR progress, orchestrator chunking, cache
> layer). Instead, hypothesis-test with a **lean offline C# harness that reads the
> cached candle files directly** (~6 min for the full universe, often faster once
> the OS file-cache is warm). Use the slow in-app run only to *validate the final
> locked config*.

**Why it's ~3× faster:** the harness skips ~90% of the machinery — it just does
`read .json file → compute indicator → check signal → tally in a List → print`.
No DB, no progress events, no chunking, no orchestrator. Pure CPU over cached files.

**How to build one** (copy `tools/vwap-diag*.cs` as templates):
- File-based C# (`dotnet run tools/<x>.cs`). Iterate `data/<seg>/<tf>/<secId>/<date>.json`.
- **Parse with `JsonDocument`, NOT `JsonSerializer.Deserialize<T>`** — .NET file-based
  apps disable reflection-based serialization by default (it throws at runtime).
- **Timestamps are mixed:** some cache files have a `Z` suffix, some don't. Parse with
  `DateTime.Parse(s, Invariant, AdjustToUniversal | AssumeUniversal)` and treat the
  value as UTC (IST = UTC + 5:30). Getting this wrong silently drops whole date ranges.
- Re-implement only the strategy's entry/exit math; tally net-R per trade.

**Non-negotiable discipline for the harness** (these are how it bites you):
- **Build non-lookahead in from the FIRST probe.** Use only data known at signal
  time — e.g. *prior-day* average volume, not today's full-day volume. A lookahead
  liquidity filter inflated one VWAP champion ~4× (claimed +0.74R / 84% months; the
  honest non-lookahead truth was +0.21R / 46%). If a "great" result appears, suspect
  lookahead first.
- **Mirror the live screener's realism:** one signal per stock-day if that's what the
  screener emits; respect the same warmup; same cost model.
- **The harness ≠ the engine.** It's an independent re-implementation, so it *will*
  disagree with the in-app engine — the engine also applies `MaxTradesPerDay` and
  `MaxCapitalPerTrade`, which the offline (uncapped) harness does not. The harness
  picks *which* config to build; the in-app run proves *what you'd actually trade*.
  Always reconcile the two before locking in (see §9).

### 8.1 In-app run (authoritative validation)

- **Run headless:** start the API (`dotnet run --project src/DhanMarketData.Api`),
  `POST /api/runs {presetId, stockCount, backtestDays, timeframe, exchangeSegment}`,
  poll `GET /api/runs/{id}` until status `2` (Completed).
- **Tune without rebuilding:** screener/strategy params live in the preset's JSON
  columns — update them in the DB and re-run. Only *code* changes (new filter
  logic / new config field) need a build + API restart.
- **Recording diagnostics:** `ScreenerSignal` has spare decimal slots
  (`RvolAtEntry`, `OrWidthPct`, `GapPct`) that the strategy writes onto each
  `Trade`. Repurpose them to the 3 features you're currently studying; document the
  remap in the screener's `MeetsSignal`. (Add more `Trade` columns if you need >3.)
- **Analysis scripts** (file-based C#, `dotnet run tools/<x>.cs`). Write small
  queries that compute, per run:
  - gross vs net + cost drag + position-size stats,
  - cross-tab by each recorded feature (n / win% / gross / avg) + keep-threshold tables,
  - cross-tab by time / day / month / risk% / hold-time,
  - drawdown, streaks, profit concentration, exit-reason breakdown, sub-buckets.
  *Copyable templates exist in `tools/` (the `ema-*.cs` set) — clone and adapt.*
- **Gotchas:**
  - **0 trades with filters off ≠ code bug** — first suspect an **expired data-API
    token** (failed cache-miss fetches return empty → screener sees no data).
  - Confirm which DB file the run and the EF tooling actually hit (a relative path
    can hit a stale copy).
  - Built-in presets can't be edited in the UI — tune via DB, lock in via an EF
    `UpdateData` migration so `BuiltInPresets.cs` ↔ model snapshot ↔ DB stay in sync.

---

## 9. Lock-in checklist (when a version passes the gates)

1. Bake the tuned config into `BuiltInPresets.cs`, with a name that *describes what
   it actually does* and a description noting the in-sample tuning.
2. Rename consistently: screener/strategy `Name`, both registries, both factory
   descriptions.
3. `dotnet ef migrations add …` → review it's a clean `UpdateData` → `database update`.
4. Build clean (0/0). Update `docs/strategies.md` and `CLAUDE.md`.
5. **Reconcile harness ↔ engine:** run the locked config in-app (the slow,
   authoritative path) and confirm the in-app result is consistent with the offline
   harness. If they diverge sharply, find out why *before* trusting either —
   usual culprits: lookahead in the harness, or `MaxTradesPerDay` /
   `MaxCapitalPerTrade` biasing the in-app sample. The in-app number is what you'd
   actually trade.
6. **Re-run the locked preset once more and confirm it reproduces** (behavior-neutral
   check) *before committing.*
7. State plainly: in-sample result + "paper-trade before risking capital."

---

## 10. Per-strategy iteration log (template)

| Run | Lever changed (one thing) | Net | PF | Win% | Max-DD | Decision / lesson |
|---|---|---|---|---|---|---|
| 1 | baseline | | | | | |
| 2 | | | | | | |
| … | | | | | | |

> Fill one row per run. The "lesson" column is the point — it's how the next
> iteration is chosen, and how a future builder learns what this strategy responds to.
