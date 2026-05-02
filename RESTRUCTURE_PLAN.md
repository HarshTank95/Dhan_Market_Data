# Convert 6_Dhan_Market_Data from Console App → Local Web App

## Context

The C# .NET 10 console backtester is currently driven entirely by `appsettings.json` — switching screener, tuning thresholds, or pointing it at a new strategy means hand-editing JSON, and any mistake silently breaks a run. We're converting it into a local web app where configs, screener/strategy selection, runs, and results are all managed through a UI, so the system stops being "hacky" to operate.

While auditing the code to plan this, I also confirmed that `PROJECT_CONTEXT.md` and `STRUCTURE.md` have drifted heavily from the actual code (full discrepancy list at the bottom of this doc). Refreshing the docs is part of this restructure.

## Decisions (locked in)

| Area | Choice |
|---|---|
| Backend | ASP.NET Core Web API (.NET 10) |
| OpenAPI | **`Microsoft.AspNetCore.OpenApi`** (built-in, generates OpenAPI 3.1). Not Swashbuckle — it was removed from default templates in .NET 9. ([source](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi)) |
| OpenAPI UI (dev-only) | **Scalar** (`Scalar.AspNetCore`) — modern, light. The JSON spec is at `/openapi/v1.json` and is what drives the typed TS client. UI is optional. |
| Frontend | Vite + React 19 + TypeScript + Tailwind v4 + shadcn/ui |
| Frontend extras | TanStack Query · TanStack Router · react-hook-form + Zod · openapi-typescript · @microsoft/signalr |
| Storage | SQLite via EF Core (`Microsoft.EntityFrameworkCore.Sqlite`, SQLite ≥ 3.46.1). WAL journaling is EF Core's default for new SQLite databases. ([source](https://github.com/dotnet/efcore/issues/14059)) |
| Token at-rest encryption | **Windows DPAPI** via `System.Security.Cryptography.ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`. Not `IDataProtectionProvider` — Microsoft's own docs say Data Protection *"isn't primarily intended for indefinite persistence of confidential payloads"* and recommend DPAPI for that. ([source](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction)) Single-user-on-Windows fits DPAPI exactly. |
| Hosting | Local single user, `localhost` only, no auth |
| Real-time progress | SignalR (broadcast run progress to UI) |
| Console app | Replaced entirely — deleted once new app is at parity |
| MVP UI features | Edit configs · Run + monitor backtests · Results dashboard |

Deferred (post-MVP): trade-chart visualization (TradingView Lightweight Charts), multi-user/auth, cloud hosting.

---

## 🔒 Behavior preservation guarantee (non-negotiable)

**The existing backtest logic is frozen.** Code can be reorganized into libraries, namespaces can change, configs can move from JSON files to a SQLite-backed UI — but **every screener criterion, every entry rule, every exit rule, every stock-picking decision, every trade produced must be byte-identical to today's console output.**

### Hard rules during the restructure

1. **Screener `.cs` files are moved verbatim.** Only namespace changes + (optionally) `[ConfigField]` attribute additions on existing properties. **Zero method-body edits.** This applies to: `VolumeSpikeScreener`, `BreakoutScreener`, `DominanceCandleScreener`, `OpeningRangeScreener`.
2. **Strategy `.cs` files are moved verbatim.** Same rule. Applies to: `FixedTargetStrategy`, `BreakoutEntryStrategy`, `TrailingStopStrategy`, `OpeningRangeBreakoutStrategy`.
3. **`BacktestEngine.cs` and `BacktestOrchestrator.cs` are moved verbatim.** The only allowed additions: `IProgress<RunProgress>` and `CancellationToken` parameters threaded through the existing loops. No reordering of the `MaxTradesPerDay → candle count → screen → entry → strategy → MaxCapitalPerTrade` sequence. `chunkSize = 30` stays. The `IstToUtc` method stays.
4. **Stock iteration order is preserved.** `InstrumentService.GetNseEquities(count)` returns CSV order; we reuse it as-is. No Linq reordering, no parallel iteration over stocks within a day (parallel chunks across runs is fine; within a run, sequential).
5. **Disk cache layout stays.** `data/{ExchangeSegment}/{Timeframe}/{SecurityId}/{Date}.json`. Cached candles remain the source of truth so re-runs produce identical inputs.
6. **Built-in strategy seed values come from current `appsettings.json` byte-for-byte**, not C# class field defaults (those have drifted in some places — e.g. `VolumeMultiplier` is `3.0m` in code but `2.0` in active config).
7. **No "drive-by improvements"** to engine/screener/strategy code during this restructure. Bug fixes that change behavior get filed separately and done **after** the regression test passes.

### Regression test (load-bearing — gates the console deletion)

Before the console app is removed, every built-in strategy must produce **byte-identical** trade output between legacy console and new web app:

1. **Baseline (Day 0)** — Run the *current* console with each of the 4 canonical configs (dominancecandle/breakoutentry, volumespike/fixedtarget, dominancecandle/trailingstop, openingrange/openingrange), 50 days each, against the existing `data/` cache. Save the 4 CSVs as `backtest_results/baseline_*.csv`. Commit them.
2. **Checkpoint after Phase 1** (libraries split, console still runs) — Re-run all 4 → diff against baselines → must be byte-identical.
3. **Checkpoint after Phase 4** (API working) — Trigger each preset via API, export CSV, diff against baselines → must be byte-identical.
4. Only when all 4 are green does Phase 6 delete the legacy console.

Diff command for verification: `git diff --no-index baseline_dominance_breakout.csv new_dominance_breakout.csv` — must produce no output.

---

## Strategy concept — single named entity per (screener + execution) combo

Each user-facing "strategy" wraps one screener + one execution strategy + their configs. Today there are **4 built-in strategies**; the design must scale to many more added later, with **zero schema migrations** when a new one ships.

### Built-in strategies (seeded from current `appsettings.json` values)

| # | Strategy name | Screener key | Execution key | Description |
|---|---|---|---|---|
| 1 | **Volume Spike** | `volumespike` | `fixedtarget` | Early-morning unusual volume; enter at 9:30 open with fixed SL/target |
| 2 | **Dominance Breakout** | `dominancecandle` | `breakoutentry` | Identify dominance candle in 9:30–10:00 window; enter on next-candle breakout above its high; fixed SL/target |
| 3 | **Dominance Trailing** | `dominancecandle` | `trailingstop` | Same entry as #2 but trailing SL instead of fixed target |
| 4 | **Opening Range Breakout** | `openingrange` | `openingrange` | Clean gap-up + opening-range structure; enter on break above OR.High in execution window |

Built-in defaults are seeded from current `appsettings.json` (the actively-tuned values), **not** the C# class field defaults — those have drifted in some cases.

Shared `TradingConfig` (overridable per strategy): `MarketOpenTime 09:15`, `MarketCloseTime 15:30`, `EntryTime 09:30`, `ExitTime 15:15`, `FixedStopLoss ₹500`, `FixedTarget ₹2000`, `TargetMultiplier 2.5`, `TrailStepMultiplier 2.0`, `MaxTradesPerDay 2`, `MaxCapitalPerTrade ₹3,00,000`.

---

## Target solution layout

Convert the single `DhanMarketData.csproj` into a multi-project solution. Existing code mostly moves verbatim into class libraries; the API project is new; the UI is a sibling folder, not a .NET project.

```
6_Dhan_Market_Data/                          # solution root
├── DhanMarketData.sln
│
├── src/
│   ├── DhanMarketData.Core/                 # class lib — shared domain
│   │   ├── Models/      Candle.cs, Trade.cs, Instrument.cs
│   │   ├── Interfaces/  IScreener.cs, IStrategy.cs
│   │   └── Configs/     BacktestConfig.cs, TradingConfig.cs,
│   │                    Screeners/{VolumeSpike,Breakout,DominanceCandle,OpeningRange}Config.cs
│   │
│   ├── DhanMarketData.Backtesting/          # class lib — screeners + strategies + engine
│   │   ├── Screeners/   (4 screeners + ScreenerFactory + IScreenerRegistry)
│   │   ├── Strategies/  (4 strategies + StrategyFactory + IStrategyRegistry)
│   │   ├── Engine/      BacktestEngine.cs, BacktestOrchestrator.cs
│   │   └── Reports/     ReportService.cs (CSV export only; JSON results go to DB)
│   │
│   ├── DhanMarketData.Infrastructure/       # class lib — external IO
│   │   ├── Api/         DhanDataApiClient.cs, DhanHistoricalResponse.cs
│   │   ├── Caching/     HistoricalDataCache.cs (file cache stays under data/)
│   │   ├── Calendar/    TradingCalendarService.cs
│   │   ├── Instruments/ InstrumentService.cs, Nifty500Stocks.cs
│   │   └── Logging/     ErrorLogger.cs
│   │
│   ├── DhanMarketData.Persistence/          # class lib — EF Core SQLite
│   │   ├── AppDbContext.cs
│   │   ├── Entities/    StrategyPreset, BacktestRun, TradeRecord, ApiCredentials
│   │   ├── Repositories/(thin wrappers used by Api)
│   │   └── Migrations/
│   │
│   └── DhanMarketData.Api/                  # web API host
│       ├── Program.cs                       # DI wiring, EF Core, SignalR, CORS for Vite
│       ├── Controllers/
│       │   ├── StrategiesController.cs      # CRUD presets + reset/clone (see API contract)
│       │   ├── RegistryController.cs        # GET available screeners + strategies + their schemas
│       │   ├── RunsController.cs            # POST start, DELETE cancel, GET list/details/trades/csv
│       │   └── CredentialsController.cs     # PUT/GET Dhan token (encrypted)
│       ├── Hubs/
│       │   └── BacktestHub.cs               # SignalR — pushes progress events per run
│       ├── BackgroundServices/
│       │   └── BacktestRunner.cs            # IHostedService consuming a Channel<RunRequest>
│       └── appsettings.json                 # only DB path + logging — NO domain config here
│
├── ui/                                       # React app (separate, not in .sln)
│   ├── package.json                         # Vite + React + TS + Tailwind + shadcn
│   ├── src/
│   │   ├── pages/
│   │   │   ├── StrategiesPage.tsx           # list, edit, reset, clone, +new (registry-driven forms)
│   │   │   ├── RunPage.tsx                  # pick preset → start run → live progress (SignalR)
│   │   │   ├── ResultsPage.tsx              # P&L, win rate, exit breakdown, trade table
│   │   │   └── CredentialsPage.tsx          # Dhan client ID + access token form
│   │   ├── api/                             # generated/typed API client (openapi-typescript)
│   │   ├── components/                      # shadcn components + custom (DynamicConfigForm)
│   │   └── hooks/useBacktestProgress.ts     # SignalR client wrapper
│   └── vite.config.ts                       # proxy /api + /hubs to localhost:5000
│
├── data/                                    # candle file cache (unchanged on disk)
├── backtest_results/                        # legacy CSVs — exports from new app land here too
├── instruments.csv
├── docs/                                    # all MD files moved here, refreshed
│   ├── README.md                            # entry point, replaces sprawl
│   ├── architecture.md                      # supersedes PROJECT_CONTEXT.md + STRUCTURE.md
│   ├── strategy-rules.md                    # merged STRATEGY_RULES + VOLUMESPIKE_STRATEGY_RULES
│   ├── data-fetching.md                     # refreshed DATA_FETCHING_GUIDE
│   └── extending.md                         # refreshed SCREENER_GUIDE + how to add strategies
└── README.md                                # short pointer to docs/
```

**Why this split:** UI talks to `Api`. `Api` depends on `Backtesting` + `Persistence` + `Infrastructure` + `Core`. `Backtesting` depends on `Core` + `Infrastructure`. `Persistence` depends on `Core`. Clean one-way dependencies; each library independently unit-testable.

---

## Key design notes

1. **Configs live in SQLite as JSON blobs on a `StrategyPreset` table — not as typed columns.**

   ```
   StrategyPreset
     Id, Name, Description, IsBuiltIn, CreatedAt, UpdatedAt
     ScreenerType    (string key, e.g. "dominancecandle")
     StrategyType    (string key, e.g. "breakoutentry")
     ScreenerConfigJson    ← serialized C# config object
     StrategyConfigJson    ← serialized C# config object (often small/empty)
     TradingConfigJson     ← serialized TradingConfig (per-preset overrides)
   ```

   **Why JSON columns, not typed-per-screener tables:** adding a new screener/strategy in the future = *zero schema migrations*. Just write the new C# config class with `[ConfigField]` attributes, register it in the factory + registry, seed one row, and the UI form auto-renders.

   Type safety lives where it should: in the C# config classes (validated on load via `JsonSerializer` against the typed class) and in the UI (Zod schemas mirroring the registry metadata).

2. **Built-in vs user presets.** `IsBuiltIn=true` rows are written by the seed migration; they cannot be deleted but **Reset to Defaults** re-applies the seed values, and **Save As New** clones to a user preset. Users can freely create/edit/delete their own presets via "+ New Strategy" — pick a screener + execution from dropdowns, name it, tweak fields.

3. **Strategy/screener metadata is exposed to the UI.** The factories already exist; we add a registry layer that returns `{ key, displayName, description, fields[] }` per type. The UI renders forms generically from that schema. Adding a new screener = create the class + register it + add a `[ConfigField]` attribute on each property → it shows up in the UI automatically. *No hard-coded forms per type.*

4. **Background runs.** API enqueues a `RunRequest` to a `Channel<T>`. A hosted `BacktestRunner` consumes the channel and runs `BacktestOrchestrator` with a `CancellationToken`. Progress events (`chunk N of M`, `day → trades count`) are pushed through the SignalR hub.

5. **Reuse existing engine code.** `BacktestEngine`, `BacktestOrchestrator`, all 4 screeners, all 4 strategies, the cache, the API client — all survive nearly unchanged. Only namespace changes + the orchestrator gains an `IProgress<RunProgress>` and `CancellationToken` parameter.

6. **Trades persisted live.** As `BacktestEngine` produces a `Trade`, the runner writes it to `TradeRecord` and emits a SignalR event. CSV export becomes a download endpoint that streams from the DB.

7. **File cache stays.** The `data/` folder is fine for a local-single-user app — moving cached candles to SQLite would bloat the DB and slow IO. Keep `HistoricalDataCache` exactly as is.

8. **Dhan token storage.** SQLite column, encrypted at rest with **Windows DPAPI** (`System.Security.Cryptography.ProtectedData.Protect` with `DataProtectionScope.CurrentUser`). The encryption key is the user's Windows login — token can only be decrypted on the same machine by the same user account. Removes the need for `appsettings.local.json`. Verified per Microsoft Data Protection docs caveat that Data Protection itself isn't primarily for indefinite at-rest secret storage.

9. **CORS + dev workflow.** API runs on `localhost:5000`, Vite on `localhost:5173`. Vite proxies `/api/*` and `/hubs/*` to the API. Single `npm run dev` + `dotnet run --project src/DhanMarketData.Api` and you're live.

---

---

## Database schema (EF Core entities)

Four tables. EF Core's SQLite provider creates new databases in **WAL journal mode by default** ([reference: dotnet/efcore#14059](https://github.com/dotnet/efcore/issues/14059)), which gives us concurrent readers + one writer with no extra config. If we ever inherit a non-WAL DB or hit issues, the canonical fallback is `Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;")` in `OnConfiguring` — *not* the connection-string `Journal Mode=WAL` keyword (it has known parser issues per [dotnet/efcore#34083](https://github.com/dotnet/efcore/issues/34083)).

```csharp
// DhanMarketData.Persistence/Entities/StrategyPreset.cs
public class StrategyPreset
{
    public int Id { get; set; }
    public string Name { get; set; } = "";              // [Index(IsUnique=true)]
    public string Description { get; set; } = "";
    public bool IsBuiltIn { get; set; }                  // false ⇒ user-created
    public string ScreenerType { get; set; } = "";      // factory key
    public string StrategyType { get; set; } = "";      // factory key
    public string ScreenerConfigJson { get; set; } = "{}";
    public string StrategyConfigJson { get; set; } = "{}";
    public string TradingConfigJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// DhanMarketData.Persistence/Entities/BacktestRun.cs
public enum RunStatus { Queued, Running, Completed, Failed, Cancelling, Cancelled }

public class BacktestRun
{
    public int Id { get; set; }
    public int StrategyPresetId { get; set; }            // [Index]
    public StrategyPreset StrategyPreset { get; set; } = null!;
    public string PresetSnapshotJson { get; set; } = ""; // frozen copy of preset at run-start (audit)
    public int StockCount { get; set; }
    public int BacktestDays { get; set; }
    public string Timeframe { get; set; } = "";
    public string ExchangeSegment { get; set; } = "";
    public RunStatus Status { get; set; }                // [Index]
    public DateTime CreatedAt { get; set; }              // [Index — for sorted listings]
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int TotalDaysProcessed { get; set; }
    public int TotalDaysPlanned { get; set; }
    public int TradeCount { get; set; }                  // denormalised, updated as trades are written
    public decimal TotalPnL { get; set; }                // denormalised
    public ICollection<TradeRecord> Trades { get; set; } = new List<TradeRecord>();
}

// DhanMarketData.Persistence/Entities/TradeRecord.cs
public class TradeRecord
{
    public long Id { get; set; }
    public int BacktestRunId { get; set; }               // [Index] — composite (BacktestRunId, Date)
    public BacktestRun BacktestRun { get; set; } = null!;
    public string Symbol { get; set; } = "";
    public string SecurityId { get; set; } = "";
    public DateTime Date { get; set; }                   // trade date (date-only)
    public DateTime EntryTime { get; set; }
    public decimal EntryPrice { get; set; }
    public int Quantity { get; set; }
    public decimal StopLoss { get; set; }
    public decimal Target { get; set; }
    public DateTime ExitTime { get; set; }
    public decimal ExitPrice { get; set; }
    public string ExitReason { get; set; } = "";        // [Index] — for exit-breakdown queries
    public decimal PnL { get; set; }
    public decimal PnLPercent { get; set; }
}

// DhanMarketData.Persistence/Entities/ApiCredentials.cs — single-row table (Id always = 1)
public class ApiCredentials
{
    public int Id { get; set; }
    public string ClientId { get; set; } = "";
    // Encrypted via Windows DPAPI — ProtectedData.Protect(token, entropy: null,
    //   DataProtectionScope.CurrentUser). Stored as base64. Decryptable only by
    //   the same Windows user account on the same machine.
    public string AccessTokenEncrypted { get; set; } = "";
    public DateTime UpdatedAt { get; set; }
}
```

**Indexes (declared in `OnModelCreating`):**
- `StrategyPreset.Name` — unique.
- `BacktestRun(StrategyPresetId)`, `BacktestRun(Status)`, `BacktestRun(CreatedAt DESC)` — listings + filtering.
- `TradeRecord(BacktestRunId, Date)` composite — paginated trade list per run.
- `TradeRecord(BacktestRunId, ExitReason)` — exit-breakdown stats.

**Why a denormalised `TradeCount` / `TotalPnL` on `BacktestRun`:** the runs list page renders fast without scanning thousands of trade rows.

**Why `PresetSnapshotJson`:** if a user edits a preset after a run completes, the historical run still shows what config it actually used.

---

## Built-in seed data (4 presets — exact JSON)

Phase 2's seed migration inserts these rows into `StrategyPreset` with `IsBuiltIn = true`. Values are byte-for-byte from the current `appsettings.json` (the actively-tuned defaults — not C# class field defaults, which have drifted).

### 1. Volume Spike
```json
{
  "name": "Volume Spike",
  "description": "Early-morning unusual volume; enter at 9:30 open with fixed SL/target",
  "screenerType": "volumespike",
  "strategyType": "fixedtarget",
  "screenerConfig": {
    "ScreeningCandleCount": 3,
    "VolumeMultiplier": 2.0,
    "CandleSizeMultiplier": 3.0
  },
  "strategyConfig": {},
  "tradingConfig": {
    "MarketOpenTime": "09:15:00", "MarketCloseTime": "15:30:00",
    "EntryTime": "09:30:00", "ExitTime": "15:15:00",
    "FixedStopLoss": 500, "FixedTarget": 2000, "TargetMultiplier": 2.5,
    "TrailStepMultiplier": 2.0, "RequireCloseAboveDayOpen": false,
    "MaxTradesPerDay": 2, "MaxCapitalPerTrade": 300000
  }
}
```

### 2. Dominance Breakout
```json
{
  "name": "Dominance Breakout",
  "description": "Identify dominance candle in 9:30–10:00 window; enter on next-candle breakout above its high; fixed SL/target",
  "screenerType": "dominancecandle",
  "strategyType": "breakoutentry",
  "screenerConfig": {
    "MinBodyPercent": 70, "MaxBodyPercent": 85, "MinWickPercent": 5,
    "MinCandleSizeMultiplier": 1.0, "MaxCandleSizeMultiplier": 2.5,
    "VolumeMultiplier": 2.0, "MinAbsoluteVolume": 5000,
    "MaxMovementMultiplier": 2.0,
    "MaxGapUpPercent": 2.5, "MaxGapDownPercent": 1.0,
    "HistoricalDays": 10,
    "EntryBracketStart": "09:30:00", "EntryBracketEnd": "10:00:00"
  },
  "strategyConfig": {},
  "tradingConfig": { /* same TradingConfig as above */ }
}
```

### 3. Dominance Trailing
Same as #2 except `"strategyType": "trailingstop"`. The trailing logic reads `TrailStepMultiplier` from `tradingConfig` (already 2.0).

### 4. Opening Range Breakout
```json
{
  "name": "Opening Range Breakout",
  "description": "Clean gap-up + opening-range structure; enter on break above OR.High in execution window",
  "screenerType": "openingrange",
  "strategyType": "openingrange",
  "screenerConfig": {
    "MinGapPercent": 0.8, "MaxGapPercent": 10.0,
    "MaxUpperWickPercent": 80, "MinVolumeMultiplier": 1.5,
    "CleanCandleCount": 2, "OpeningRangeMinutes": 10,
    "ObservationEndTime": "09:25:00",
    "ExecutionWindowStart": "09:40:00", "ExecutionWindowEnd": "09:40:00",
    "HistoricalDaysForAverage": 10, "MaxCandleSizeMultiplier": 3.0
  },
  "strategyConfig": {},
  "tradingConfig": { /* same TradingConfig as above */ }
}
```

The seed code lives in `DhanMarketData.Persistence/Migrations/SeedBuiltInPresets.cs`. **Reset to Defaults** in the UI re-runs this seed for a single preset row (overwrites JSON columns, leaves Id intact).

---

## API contract

All routes prefixed `/api`. Errors use RFC 7807 problem-detail JSON. Auth: none (localhost-only). All requests/responses are JSON unless noted.

| Method | Route | Request body | Response | Notes |
|---|---|---|---|---|
| GET | `/strategies` | — | `StrategyPresetSummary[]` | Includes Name, Description, IsBuiltIn, ScreenerType, StrategyType, UpdatedAt |
| GET | `/strategies/{id}` | — | `StrategyPresetDetail` | Adds parsed configs (typed objects, not raw JSON strings) |
| POST | `/strategies` | `CreateStrategyPresetRequest` | `StrategyPresetDetail` (201) | User preset only; validates against registry schema |
| PUT | `/strategies/{id}` | `UpdateStrategyPresetRequest` | `StrategyPresetDetail` | **400 if `IsBuiltIn=true`** — user must clone or reset instead |
| DELETE | `/strategies/{id}` | — | 204 | **400 if `IsBuiltIn=true`** |
| POST | `/strategies/{id}/reset` | — | `StrategyPresetDetail` | Re-applies seed values; only valid for built-ins |
| POST | `/strategies/{id}/clone` | `{ name: string }` | `StrategyPresetDetail` (201) | Creates user preset copy with same configs |
| GET | `/registry/screeners` | — | `RegistryEntry[]` | See registry contract below |
| GET | `/registry/strategies` | — | `RegistryEntry[]` | Same |
| GET | `/credentials` | — | `{ clientId: string, hasToken: boolean }` | **Never returns plaintext token** |
| PUT | `/credentials` | `{ clientId, accessToken }` | `{ clientId, hasToken }` | Encrypts before insert/update |
| POST | `/runs` | `StartRunRequest` | `{ runId: int }` (202) | Queues background run |
| GET | `/runs` | query: `?status=&limit=&offset=` | `BacktestRunSummary[]` | Sorted CreatedAt DESC |
| GET | `/runs/{id}` | — | `BacktestRunDetail` | Includes summary stats from denormalised columns |
| GET | `/runs/{id}/trades` | query: `?page=&pageSize=&exitReason=` | `{ trades: TradeRecord[], totalCount: int }` | Paged |
| GET | `/runs/{id}/csv` | — | `text/csv` stream | **Same column order as legacy `backtest_results/*.csv`** |
| DELETE | `/runs/{id}` | — | 204 | Cancellation; idempotent (already-cancelled returns 204 too) |

`StartRunRequest`:
```json
{
  "presetId": 2,
  "stockCount": 500,
  "backtestDays": 50,
  "timeframe": "5min",
  "exchangeSegment": "NSE_EQ"
}
```

**SignalR hub** at `/hubs/backtest`. Client joins group `run-{id}` after starting a run. Events:

| Event | Payload | When |
|---|---|---|
| `RunStarted` | `{ runId, totalDaysPlanned }` | Run dequeued, status → Running |
| `ChunkProgress` | `{ runId, chunkNumber, totalChunks, daysProcessed }` | After each 30-day chunk |
| `TradeRecorded` | `{ runId, trade: TradeRecord }` | After each trade is persisted |
| `RunCompleted` | `{ runId, summary: { totalPnL, tradeCount, winRate, exitBreakdown } }` | Run finished cleanly |
| `RunFailed` | `{ runId, errorMessage }` | Unhandled exception or fatal Dhan error |
| `RunCancelled` | `{ runId, daysProcessed, daysPlanned }` | Cancellation completed |

---

## Registry contract (`[ConfigField]` attribute → JSON schema → UI form)

The "registry" is what makes new strategies plug in without UI code changes. Reflection over config classes produces the JSON the UI consumes.

### Attribute definition

```csharp
// DhanMarketData.Core/Configs/Attributes/ConfigFieldAttribute.cs
[AttributeUsage(AttributeTargets.Property)]
public class ConfigFieldAttribute : Attribute
{
    public string Label { get; init; } = "";
    public string? Description { get; init; }            // tooltip text
    public string Group { get; init; } = "General";     // form section heading
    public ConfigFieldKind Kind { get; init; } = ConfigFieldKind.Auto;
    public double Min { get; init; } = double.NaN;       // NaN ⇒ no constraint
    public double Max { get; init; } = double.NaN;
    public double Step { get; init; } = double.NaN;
    public string? Unit { get; init; }                   // e.g. "%", "₹", "x"
    public int Order { get; init; } = 0;                 // form field order within group
}

public enum ConfigFieldKind
{
    Auto, Number, Percent, Currency, Multiplier, TimeOfDay, Boolean, Text, Integer
}
```

### Example application

```csharp
public class DominanceCandleConfig
{
    [ConfigField(Label = "Min Body %", Group = "Body Shape",
                 Description = "Minimum body size as % of candle range",
                 Kind = ConfigFieldKind.Percent, Min = 0, Max = 100)]
    public decimal MinBodyPercent { get; set; } = 70m;

    [ConfigField(Label = "Volume Multiplier", Group = "Volume",
                 Kind = ConfigFieldKind.Multiplier, Min = 0.5, Step = 0.1, Unit = "x")]
    public decimal VolumeMultiplier { get; set; } = 1.5m;

    [ConfigField(Label = "Entry Window Start", Group = "Time",
                 Kind = ConfigFieldKind.TimeOfDay, Order = 1)]
    public TimeSpan EntryBracketStart { get; set; } = new(9, 15, 0);

    [ConfigField(Label = "Entry Window End", Group = "Time",
                 Kind = ConfigFieldKind.TimeOfDay, Order = 2)]
    public TimeSpan EntryBracketEnd { get; set; } = new(9, 45, 0);
    // ...
}
```

### Registry output (consumed by UI)

`GET /api/registry/screeners` returns:

```json
[
  {
    "key": "dominancecandle",
    "displayName": "Dominance Candle",
    "description": "Identifies strong directional candles with body dominance",
    "configClassName": "DominanceCandleConfig",
    "fields": [
      {
        "name": "MinBodyPercent", "label": "Min Body %", "group": "Body Shape",
        "description": "Minimum body size as % of candle range",
        "kind": "percent", "min": 0, "max": 100, "default": 70, "order": 0
      },
      {
        "name": "VolumeMultiplier", "label": "Volume Multiplier", "group": "Volume",
        "kind": "multiplier", "min": 0.5, "step": 0.1, "unit": "x",
        "default": 1.5, "order": 0
      },
      {
        "name": "EntryBracketStart", "label": "Entry Window Start", "group": "Time",
        "kind": "timeofday", "default": "09:15:00", "order": 1
      }
    ]
  },
  { "key": "volumespike", "displayName": "Volume Spike", ... },
  { "key": "openingrange", ... },
  { "key": "breakout", ... }
]
```

### How the UI consumes it

`DynamicConfigForm.tsx` — single component, used by all strategy edit pages:

1. `useQuery(['registry', 'screeners'])` to fetch field metadata.
2. Build a Zod schema dynamically from the field list (`min`/`max` → `.min().max()`, `kind: percent` → `.number()`).
3. Render shadcn `Form` with react-hook-form, one `FormField` per field, grouped by `group`. Field component picked by `kind`:
   - `percent` / `number` / `integer` / `multiplier` / `currency` → `<Input type="number">`
   - `timeofday` → `<Input type="time">`
   - `boolean` → `<Switch>`
   - `text` → `<Input type="text">`
4. On submit → `PUT /api/strategies/{id}` with the form values. Server re-validates server-side using the same registry schema before persisting.

**Adding a new screener post-launch:**
1. Write `MyNewScreener.cs` + `MyNewConfig.cs` (decorate with `[ConfigField]`).
2. Add factory key + registry entry.
3. Insert one row into `StrategyPreset` (or let user create via "+ New Strategy").

UI updates automatically on next refresh. Zero front-end code changes.

---

## Dhan API — verified vs unverified facts

What the [official Dhan v2 docs](https://dhanhq.co/docs/v2/historical-data/) actually say (verified):

- **Supported intraday intervals**: `1`, `5`, `15`, `25`, `60` minutes (exact quote: *"Minute intervals in timeframe `1`, `5`, `15`, `25`, `60`"*). Matches the `HistoricalDataCache` switch statement.
- **NEW constraint** (not in current code or old docs): *"only 90 days of data can be polled at once for any of the above time intervals"* for intraday. Daily candles have no such cap. Our current orchestrator fetches one trading day per API call so we don't hit this — but **any future bulk-fetch optimisation must respect the 90-day cap.** Worth a code-comment in `DhanDataApiClient`.
- **Daily historical**: *"available back upto the date of its inception"* — no day cap.

What's in the existing code/docs but **not verified from current public Dhan v2 docs**:

| Claim in code/old docs | Where it appears | Verification status |
|---|---|---|
| "Rate limit 5 req/sec, we use 4 req/sec (250 ms throttle)" | `DhanDataApiClient.cs`, PROJECT_CONTEXT.md | Not in v2 docs (the rate-limit page returns 404 as of this writing). Treat as community/empirical knowledge — keep the 250 ms throttle as-is, don't tighten. |
| `DH-905` error means "no data for delisted/suspended stock" | `DhanDataApiClient.cs` silently swallows it | Not in v2 docs (errors page returns 404). Existing handling is empirical; preserve verbatim. |
| Access token validity ~5 days | PROJECT_CONTEXT.md | Not in v2 docs. UI should treat token as opaque and surface auth errors when they happen rather than relying on a hard-coded TTL. |

**Action**: don't repeat unverified numbers in new docs. Keep the existing throttle/error handling untouched (behavior preservation rule). When refreshing `docs/data-fetching.md`, link to current Dhan docs rather than restating numbers we can't source.

---

## Operational semantics (runs, errors, concurrency)

### Run lifecycle
```
Queued ──► Running ──► Completed
              │  └────► Failed
              └──► Cancelling ──► Cancelled
```

### Concurrency policy
**Single concurrent run.** `BacktestRunner` hosted service has one consumer over a `Channel<RunRequest>` (capacity 10). Additional `POST /api/runs` calls enqueue and return immediately with 202. UI shows queue position via SignalR.

Rationale: Dhan API rate limit is 5 req/sec; running two backtests in parallel would force complex shared throttling. Future-proofable — can lift to N parallel workers if Dhan throttle is moved to a shared semaphore.

### Cancellation semantics
- `DELETE /api/runs/{id}` → if `Queued`: status → `Cancelled` immediately, removed from channel. If `Running`: status → `Cancelling`, `CancellationTokenSource.Cancel()` fires.
- `BacktestOrchestrator` checks the token at chunk boundaries and at the top of each day's stock loop. **Cancellation is graceful** — the in-flight day finishes, then loop exits.
- **Trades produced before cancellation are kept** — they're real backtest output, just for partial coverage. `TotalDaysProcessed < TotalDaysPlanned` flags it as partial. UI displays "Partial — cancelled at day X of Y".
- DELETE is **idempotent**: calling on an already-`Cancelled`/`Completed` run returns 204, no error.

### Failure modes
| Failure | Detection | Response |
|---|---|---|
| Dhan returns DH-905 (delisted) | Already handled in `DhanDataApiClient` | Empty list, run continues. **No status change.** |
| Dhan auth error (token expired/invalid) | HTTP 401/403 | Run → `Failed`, `ErrorMessage` = "Dhan token expired or invalid". UI shows toast → CredentialsPage. |
| Dhan rate-limit (429 despite our 250 ms throttle) | HTTP 429 | Exponential backoff retry up to 3× (1 s, 2 s, 4 s). If still failing, run → `Failed`. |
| SQLite locked | `SqliteException` with `SQLITE_BUSY` | WAL mode prevents this for reader/writer mix; if it still occurs, retry insert up to 3× with 100 ms backoff. |
| Server crashes mid-run | On API startup | Scan rows where `Status IN (Running, Cancelling)` → mark `Failed`, `ErrorMessage` = "Server restarted during run". User can re-queue. |
| Unhandled exception in orchestrator | Outer try/catch in `BacktestRunner` | Run → `Failed`, full exception message + stack saved to `error_log.txt` via existing `ErrorLogger`. |

### Network safety (single-user local)
- Kestrel binds to **`127.0.0.1` only** (not `0.0.0.0`) — not reachable from LAN.
- CORS allows only `http://localhost:5173` (Vite dev) and same-origin in prod build.
- Dhan token never logged, never returned plaintext over `/api/credentials` GET.

---

## Implementation phases

### Phase 0 — Capture baselines (before any code moves)
- Run the *current* console app with each of the 4 canonical configs:
  - `volumespike` + `fixedtarget`
  - `dominancecandle` + `breakoutentry`
  - `dominancecandle` + `trailingstop`
  - `openingrange` + `openingrange`
- 50 backtest days each, against the existing `data/` cache.
- Save 4 CSVs as `backtest_results/baseline_<screener>_<strategy>.csv` and **commit them** to the repo. These are the regression contract.

### Phase 1 — Solution restructure (no behavior change)
- Create `src/` folders and the 5 class library + API csproj files.
- Move existing code into the right libraries; **namespace updates only** — no method-body edits anywhere in `Screeners/`, `Strategies/`, `Backtest/`. (See "Behavior preservation guarantee" rules 1–4.)
- Solution still builds; console `Program.cs` retained as a thin wrapper that calls into the libraries so the smoke-test still works.
- **Checkpoint:** re-run all 4 baseline configs through the still-working console → diff vs `baseline_*.csv` → must be byte-identical. Don't proceed to Phase 2 until green.
- Bug-fix backlog (filed for *after* parity is achieved, not done now): `BacktestOrchestrator` line ~44 prints `_backtestEngine.GetType().Name` instead of `_strategy.Name`. Display-only — doesn't affect trades — and the line goes away anyway when the console is removed in Phase 6.

### Phase 2 — Persistence layer
- Add `DhanMarketData.Persistence` with EF Core SQLite + migrations.
- Entities: `StrategyPreset` (with 3 JSON config columns), `BacktestRun`, `TradeRecord`, `ApiCredentials`, `AppSettings` (any cross-cutting settings).
- **Seed migration** inserts the 4 built-in `StrategyPreset` rows (Volume Spike · Dominance Breakout · Dominance Trailing · Opening Range Breakout) using the values currently in `appsettings.json` so day-1 the DB matches today's behavior.
- Repositories expose typed CRUD — thin, no business logic.

### Phase 3 — Registry + schema
- `IScreenerRegistry` / `IStrategyRegistry` enumerate available types and yield field metadata via reflection over `[ConfigField(label, default, min, max, kind)]` attributes on the existing config classes.
- `RegistryController` exposes `/api/registry/screeners` and `/api/registry/strategies`.

### Phase 4 — API surface
- `StrategiesController` — `GET /api/strategies` (list presets), `GET /api/strategies/{id}`, `POST` (create), `PUT` (update), `DELETE` (user presets only), `POST /api/strategies/{id}/reset` (built-ins → re-seed defaults), `POST /api/strategies/{id}/clone`.
- `RegistryController` — `GET /api/registry/screeners`, `GET /api/registry/strategies` (returns key + display name + description + field schema for form rendering).
- `CredentialsController` — set/check Dhan token.
- `RunsController` — `POST /api/runs` (body: `{ presetId, stockCount, backtestDays, timeframe }`), `DELETE /api/runs/{id}` (cancel), `GET /api/runs`, `GET /api/runs/{id}`, `GET /api/runs/{id}/trades`, `GET /api/runs/{id}/csv`.
- Background `BacktestRunner` hosted service + `Channel<RunRequest>` queue.
- `BacktestHub` SignalR endpoint with per-run group; events: `RunStarted`, `ChunkProgress`, `TradeRecorded`, `RunCompleted`, `RunFailed`, `RunCancelled`.
- Adapt `BacktestOrchestrator` to accept `IProgress<RunProgress>` + `CancellationToken`; emit events from there.

### Phase 5 — React UI
- Scaffold `ui/` with Vite + React 19 + TS + Tailwind v4 + shadcn (`npx shadcn@latest init`).
- Add: TanStack Query (server state), TanStack Router (type-safe routing), react-hook-form + Zod (forms), `@microsoft/signalr` (live progress).
- Generate API types from the **`Microsoft.AspNetCore.OpenApi`** spec (`openapi-typescript` + `openapi-fetch`). The spec lives at `/openapi/v1.json`; **avoid the deprecated `WithOpenApi()` extension** (.NET 10 emits `ASPDEPR002`).
- **StrategiesPage** — list of presets (4 built-in + user-created). Click → form rendered from `/api/registry` schema, all fields editable, **Reset / Save / Save As New / Clone / Delete** buttons. **+ New Strategy** dropdown picks a screener + execution combo and pre-fills defaults.
- **RunPage** — pick preset from dropdown → set stock count + days + timeframe → "Start" button → progress bar + live trade list via SignalR.
- **ResultsPage** — runs table → click run → P&L summary card, exit-breakdown chart, sortable/filterable trade table, "Download CSV" button.
- **CredentialsPage** — Dhan client ID + access token form.

### Phase 5.5 — Regression checkpoint (gates Phase 6)
- Trigger each of the 4 built-in presets via the API at 50 days, against the same `data/` cache.
- Export trades CSV from the API for each.
- Diff each against the corresponding `backtest_results/baseline_*.csv` from Phase 0.
- **All 4 must be byte-identical.** If any differ — stop, find the drift, fix it before Phase 6.

### Phase 6 — Retire console app + refresh docs (only if Phase 5.5 is green)
- Delete the original `Program.cs` console entry (the API project becomes the only host).
- Move all 6 root-level MD files into `docs/`, refresh content per discrepancy list, collapse overlapping ones into the structure shown above.
- Top-level `README.md` becomes a short "what + how to run" pointer to `docs/`.
- Keep the `baseline_*.csv` files in the repo — they're the historical contract for any future logic change ("if you change a screener, you must explain why baseline diffed").

---

## Critical files (create / modify / delete)

**Create**
- `DhanMarketData.sln` (regenerate)
- `src/DhanMarketData.{Core,Backtesting,Infrastructure,Persistence,Api}/*.csproj`
- `src/DhanMarketData.Persistence/AppDbContext.cs` + Entities + Migrations
- `src/DhanMarketData.Api/Program.cs`, `Controllers/*.cs`, `Hubs/BacktestHub.cs`, `BackgroundServices/BacktestRunner.cs`
- `src/DhanMarketData.Backtesting/Screeners/IScreenerRegistry.cs` + impl
- `src/DhanMarketData.Backtesting/Strategies/IStrategyRegistry.cs` + impl
- `src/DhanMarketData.Core/Configs/Attributes/ConfigFieldAttribute.cs`
- `ui/` (entire React project)
- `docs/{README,architecture,strategy-rules,data-fetching,extending}.md`
- Top-level `README.md`

**Modify (touched files — all namespace-only edits except where noted)**
- All `Screeners/*.cs`, `Strategies/*.cs`, `Backtest/BacktestEngine.cs` — **namespace only, zero logic changes** (behavior preservation rules 1–3).
- `Backtest/BacktestOrchestrator.cs` — namespace + thread `IProgress<RunProgress>` and `CancellationToken` through existing loops. **No reordering, no parallelization, `chunkSize=30` stays.**
- `Configs/ScreenerConfigs.cs` → split per type into `Configs/Screeners/*.cs`, add `[ConfigField]` attributes (decoration only — no value changes).
- `Configs/BacktestConfig.cs` → split `BacktestConfig` and `TradingConfig` into separate files, add `[ConfigField]` attributes.
- `Infrastructure/*` — namespace only, no logic changes (cache, API client, calendar, instrument loader, error logger all stay verbatim).

**Delete**
- Old root-level `Program.cs`
- Old root-level `DhanMarketData.csproj`
- Old root-level `appsettings.json` + `appsettings.local.json[.template]` (token moves to DB)
- Old root-level MD files (after content is migrated to `docs/`)

---

## Verification

End-to-end smoke test once Phase 5 is complete:

1. `dotnet build` — solution builds clean.
2. `dotnet ef database update --project src/DhanMarketData.Persistence --startup-project src/DhanMarketData.Api` — DB created, seed data present.
3. Two terminals: `dotnet run --project src/DhanMarketData.Api` and `cd ui && npm run dev`.
4. Open `http://localhost:5173`. Open ConfigsPage → enter Dhan token → save → confirm row in `ApiCredentials` table (encrypted).
5. ConfigsPage → DominanceCandle tab → tweak `MinBodyPercent` → save → reopen → value persisted.
6. RunPage → pick the dominancecandle/breakoutentry profile → click Start → progress bar updates live, trades stream into the right pane.
7. ResultsPage → totals match what the legacy console run produced for the same config + same data range (use the existing `data/` cache to keep candles identical).
8. Cancel a run mid-way → status updates to `Cancelled`, no orphan rows.
9. Download CSV from a completed run → diff against `backtest_results/{screener}_{strategy}.csv` produced by the old console for the same inputs → identical trades.
10. `dotnet test` (if/when tests are added) — all green.

Step 7 + step 9 are the load-bearing checks: they prove no behavior regressed during the migration.

---

## Appendix — Technical claims & validation sources

Every technical claim in this plan is in one of three buckets. This is the audit trail.

### ✅ Verified from official documentation
| Claim | Source |
|---|---|
| Dhan v2 intraday intervals are `1, 5, 15, 25, 60` min | [Dhan v2 historical-data docs](https://dhanhq.co/docs/v2/historical-data/) |
| Dhan v2 intraday: max 90 days per call | [Dhan v2 historical-data docs](https://dhanhq.co/docs/v2/historical-data/) |
| .NET 10 default OpenAPI is `Microsoft.AspNetCore.OpenApi` (not Swashbuckle) | [MS Learn — OpenAPI in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi) |
| `WithOpenApi()` deprecated in .NET 10 (ASPDEPR002) | [MS Learn — WithOpenApi deprecation](https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/withopenapi-deprecated?view=aspnetcore-10.0) |
| Swashbuckle removed from default templates in .NET 9 | [dotnet/aspnetcore#54599](https://github.com/dotnet/aspnetcore/issues/54599) |
| EF Core SQLite creates new DBs in WAL by default | [dotnet/efcore#14059](https://github.com/dotnet/efcore/issues/14059) |
| `Journal Mode=WAL` in connection string is unreliable; use `PRAGMA` instead | [dotnet/efcore#34083](https://github.com/dotnet/efcore/issues/34083) |
| EF Core SQLite requires SQLite ≥ 3.46.1 | [MS Learn — EF Core SQLite provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/) |
| ASP.NET Core Data Protection caveat on long-term secret storage | [MS Learn — Data Protection introduction](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/introduction) |
| `IDataProtectionProvider` / `IDataProtector` interfaces exist in `Microsoft.AspNetCore.DataProtection.Abstractions` | Same |

### ⚠️ Empirical / carried-from-existing-code (not in current public docs)
| Claim | Why kept anyway |
|---|---|
| Dhan rate limit ~5 req/sec; we use 250 ms throttle (≈4 req/sec) | Existing `DhanDataApiClient` uses this; behavior-preservation rule says don't change it. Public rate-limit doc page currently 404s. |
| `DH-905` = "no data for delisted/suspended stock" | Empirical from existing `DhanDataApiClient` error handling. Public errors doc page currently 404s. |
| Dhan access token validity ~5 days | From old `PROJECT_CONTEXT.md`. Treat token as opaque; surface auth errors if/when they occur. |

### 🔒 Project-internal (no external source — verified by code reading)
| Claim | Verified by |
|---|---|
| 4 screeners + 4 strategies + factories | `Screeners/*.cs`, `Strategies/*.cs`, factory files |
| Cache layout `data/{ExchangeSegment}/{Timeframe}/{SecurityId}/{Date}.json` | `HistoricalDataCache.cs` |
| Backtest chunk size = 30 days | `BacktestOrchestrator.cs` |
| `MaxTradesPerDay → candle-count → screen → entry → strategy → MaxCapitalPerTrade` order | `BacktestOrchestrator.ExecuteBacktestAsync` |
| IST→UTC conversion in `BacktestEngine.IstToUtc` | `BacktestEngine.cs` |
| Built-in seed JSON values | Current `appsettings.json` byte-for-byte |
| Trading calendar: weekdays + hardcoded NSE holidays 2025–2026 | `TradingCalendarService.cs` |

### 📌 Rule for this plan going forward
Any new technical assertion added to this doc must cite either an official docs URL, a GitHub issue/release note, or "verified from code at `<path>:<line>`". No bare claims.

---

## Appendix — MD-vs-code discrepancies (cleanup queue)

These were found during the audit. Each one needs to be fixed when we rewrite docs in Phase 6.

**`PROJECT_CONTEXT.md` — most stale**
1. Wrong namespaces — claims `Services/` for screeners/strategies; actual locations are `Screeners/`, `Strategies/`, `Backtest/Reports/`, `Calendar/`, `Infrastructure/Data/`, `Core/Interfaces/`.
2. Missing entire features: `OpeningRangeScreener`, `OpeningRangeBreakoutStrategy`, `OpeningRangeConfig`.
3. Wrong strategy key: doc says `"breakout"`; factory expects `"breakoutentry"`.
4. `VolumeSpikeConfig` field names invented (no `MinVolumeMultiplier` / `HistoricalDaysForAverage` / gap fields exist on the class).
5. Dominance criteria described in the doc (body ≥ 1.5%, wicks ≤ 0.25/0.35%, body/range ≥ 0.9) **don't match code** (body 70–85% of range, wicks ≥ 5%, size 1.0–2.5× avg).
6. `BreakoutConfig` field names wrong (`MinConsolidationCandles` / `MaxRangePercent` don't exist).
7. `MaxCapitalPerTrade` (in `TradingConfig`) not mentioned.
8. "Current Configuration" snapshot wrong (says 60min + DataFetchOnly=true + volumespike; actual is 5min + DataFetchOnly=false + dominancecandle + breakoutentry).

**`STRUCTURE.md`**
- Layout is right, but missing `TrailingStopStrategy`, `OpeningRangeBreakoutStrategy`, `OpeningRangeScreener`, `StrategyFactory`. Combinations table incomplete.

**`DATA_FETCHING_GUIDE.md`**
- Mentions 4-hour timeframe usage. Code throws on it (no `"4hour"` case in `HistoricalDataCache`).

**`STRATEGY_RULES.md` / `VOLUMESPIKE_STRATEGY_RULES.md`**
- Mostly accurate today. Will need only minor edits when we move them under `docs/` and merge.

**`SCREENER_GUIDE.md`**
- Very thin (~70 lines). Doesn't show `IScreener` interface or current factory. Worth rewriting to cover both screeners and strategies.

**Bug found incidentally**
- `Backtest/BacktestOrchestrator.cs` line ~44: `Console.WriteLine($"Strategy: Target={_backtestEngine.GetType().Name}");` — prints `"BacktestEngine"` as the strategy name. Should be `_strategy.Name`. Fix in Phase 1.
