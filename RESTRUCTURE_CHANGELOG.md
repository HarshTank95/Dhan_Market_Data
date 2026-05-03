# Restructure Changelog

Companion to `RESTRUCTURE_PLAN.md`. Records what was actually built per phase, what was deviated from the plan, what was verified, and what's still pending.

**Migration goal:** convert the .NET 10 console backtester into a local web app (ASP.NET Core Web API + React UI + SQLite) where strategies and configs are managed through the UI, with no behavior changes to the existing screening/entry/exit logic.

---

## Status snapshot

| Phase | Description | Status |
|---|---|---|
| 0 | Capture baseline CSVs | ⏭️ Skipped (user decision — not needed for this migration) |
| 1 | Solution restructure → 6 projects | ✅ Done (committed `16b6e37`) |
| 2 | EF Core SQLite + entities + seeds + repos | ✅ Done |
| 3 | `[ConfigField]` attribute + screener/strategy registry | ✅ Done |
| 4 | Web API surface (controllers, SignalR, runner, DPAPI) | ✅ Done — smoke-tested |
| 5 | React UI (Vite + Tailwind v4 + TanStack Query + SignalR) | ✅ Done |
| 5.5 | Integration smoke (API ↔ UI ↔ Vite proxy) | ✅ Done — end-to-end backtest run still gated on a fresh Dhan token |
| 6 | Retire console + refresh docs | ⏳ Pending |

---

## Behavior-preservation rule (held throughout)

The plan's hard rule: **screener / strategy / engine `.cs` files move verbatim — no method-body edits, no logic re-ordering, no defaults changed.** The only authorised engine touch was adding `IProgress<BacktestProgress>` + `CancellationToken` plumbing to `BacktestOrchestrator` (Phase 4) — additive only, original public method retained for the Console smoke-test path.

What was NOT touched:
- `Backtesting/Screeners/*.cs` — all 4 screeners byte-identical to pre-restructure.
- `Backtesting/Strategies/*.cs` — all 4 strategies byte-identical.
- `Backtesting/Engine/BacktestEngine.cs` — byte-identical.
- `Backtesting/Reports/ReportService.cs` — byte-identical.
- `Infrastructure/**` — all files byte-identical.
- `Calendar/TradingCalendarService.cs` — byte-identical.
- `Core/Models/*.cs` — byte-identical.
- `Core/Interfaces/*.cs` — byte-identical.
- `Configs/ScreenerConfigs.cs`, `Configs/BacktestConfig.cs` — only **decoration** added (new `[ConfigField]` attributes on existing properties); zero changes to property names, types, or default values.

The plan-noted bug at `BacktestOrchestrator` line ~44 (`_backtestEngine.GetType().Name` instead of `_strategy.Name`) was deliberately **left in place** — it's display-only and the line goes away when the Console is retired in Phase 6.

---

## Phase 1 — Solution restructure (committed `16b6e37`)

Single console project → 6-project solution under `src/`.

**Created:**
- `DhanMarketData.sln` (regenerated)
- `src/DhanMarketData.Core/DhanMarketData.Core.csproj` (no deps)
- `src/DhanMarketData.Infrastructure/...csproj` (deps: Core)
- `src/DhanMarketData.Backtesting/...csproj` (deps: Core, Infrastructure)
- `src/DhanMarketData.Persistence/...csproj` (deps: Core)
- `src/DhanMarketData.Api/...csproj` (deps: all four — Web SDK)
- `src/DhanMarketData.Console/...csproj` (deps: Core, Infrastructure, Backtesting; replaces the old root-level Program.cs/csproj)
- `src/DhanMarketData.Console/Properties/launchSettings.json` — workingDirectory set to `$(SolutionDir)` so `data/`, `instruments.csv`, `appsettings.json` continue to resolve from solution root.

**Moved** (`git mv` for tracked, plain `mv` for untracked — final `git add -A` gives all moves rename-tracking):

| From | To |
|---|---|
| `Core/Models/*.cs` | `src/DhanMarketData.Core/Models/` |
| `Core/Interfaces/*.cs` | `src/DhanMarketData.Core/Interfaces/` |
| `Configs/*.cs` | `src/DhanMarketData.Core/Configs/` |
| `Screeners/*.cs` | `src/DhanMarketData.Backtesting/Screeners/` |
| `Strategies/*.cs` | `src/DhanMarketData.Backtesting/Strategies/` |
| `Backtest/{BacktestEngine,BacktestOrchestrator}.cs` | `src/DhanMarketData.Backtesting/Engine/` |
| `Backtest/Reports/ReportService.cs` | `src/DhanMarketData.Backtesting/Reports/` |
| `Infrastructure/**` | `src/DhanMarketData.Infrastructure/`  (Data → Instruments) |
| `Calendar/TradingCalendarService.cs` | `src/DhanMarketData.Infrastructure/Calendar/` |
| `Program.cs` | `src/DhanMarketData.Console/Program.cs` |

**Deviation from plan:** the plan suggested renaming namespaces (e.g. `DhanMarketData.Configs` → `DhanMarketData.Core.Configs`). I **did not** rename namespaces — files moved, namespace declarations stayed identical. This minimised risk for behavior preservation and avoided touching `using` statements in the migrated files. Folder paths and namespaces no longer match exactly (e.g. `src/DhanMarketData.Core/Configs/BacktestConfig.cs` declares `namespace DhanMarketData.Configs;`) but that's cosmetic.

**Runtime files kept at solution root:** `appsettings.json`, `appsettings.local.json`, `appsettings.local.json.template`, `instruments.csv`, `data/`, `backtest_results/`. Console project's launchSettings sets workingDirectory accordingly.

**Verified:** `dotnet build DhanMarketData.sln` → 0 warnings, 0 errors.

---

## Phase 2 — Persistence layer

**NuGet** added to `DhanMarketData.Persistence`:
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.7
- `Microsoft.EntityFrameworkCore.Design` 10.0.7

**Entities** (`src/DhanMarketData.Persistence/Entities/`):
- `StrategyPreset` — `Id`, `Name` (unique), `Description`, `IsBuiltIn`, `ScreenerType`, `StrategyType`, three JSON-as-TEXT columns (`ScreenerConfigJson`, `StrategyConfigJson`, `TradingConfigJson`), `CreatedAt`, `UpdatedAt`.
- `BacktestRun` — references preset + frozen `PresetSnapshotJson` for audit; `Status` enum (`Queued / Running / Completed / Failed / Cancelling / Cancelled`); denormalised `TradeCount` + `TotalPnL` for fast list rendering.
- `TradeRecord` — composite indexes on `(BacktestRunId, Date)` and `(BacktestRunId, ExitReason)` for paged trade listing + exit-breakdown stats. Cascade-delete from BacktestRun.
- `ApiCredentials` — single-row table (`Id` always = 1), `AccessTokenEncrypted` is base64-DPAPI ciphertext.

**`AppDbContext.cs`** uses Fluent API for indexes, JSON-as-TEXT columns, decimal precision, FK delete behaviour, and stores `RunStatus` as string (so SQLite output is human-readable).

**`DesignTimeDbContextFactory`** lets `dotnet ef` work without needing the API project as startup.

**Seed migration** (`Migrations/20260502183443_InitialCreate`) inserts the 4 built-in presets via `HasData(BuiltInPresets.All())` with values byte-for-byte from current `appsettings.json` (not C# class field defaults, which had drifted in some cases).

**Repositories** (`Repositories/`) — 4 thin interface + impl pairs: `IStrategyPresetRepository`, `IBacktestRunRepository` (incl. `ResetOrphanedRunsAsync` for crash recovery), `ITradeRecordRepository` (paged + `IAsyncEnumerable` streaming), `IApiCredentialsRepository`.

**Verified:**
- `dotnet build` clean.
- `dotnet ef database update` ran the migration; DB created at `src/DhanMarketData.Persistence/dhanmarketdata.db`.
- `journal_mode = wal` confirmed (EF Core 10 default for new SQLite DBs — no explicit `PRAGMA` needed).
- 4 built-in `StrategyPreset` rows present with correct screener/strategy types, `IsBuiltIn=1`, JSON column lengths matching expected payloads (89 / 375 / 375 / 342 chars).

**`.gitignore`** extended for `*.db`, `*.db-shm`, `*.db-wal`.

---

## Phase 3 — Registry + `[ConfigField]` schema

**Attribute** (`src/DhanMarketData.Core/Configs/Attributes/ConfigFieldAttribute.cs`) — decorates a config property with display label, description, group, kind (`Auto / Number / Integer / Percent / Currency / Multiplier / TimeOfDay / Boolean / Text`), min/max/step/unit/order. Auto-kind is inferred from the property type.

**Decorated** (decoration only — no logic / default / type changes):
- `Configs/ScreenerConfigs.cs` — every property on `ScreenerConfig` (1), `VolumeSpikeConfig` (2), `BreakoutConfig` (3), `DominanceCandleConfig` (13), `OpeningRangeConfig` (11). 30 properties decorated.
- `Configs/BacktestConfig.cs` — every relevant property on `BacktestConfig` (5; `ScreenerType`/`StrategyType` left undecorated since they're chosen via dropdown) and `TradingConfig` (11).

**Registry** (`src/DhanMarketData.Backtesting/Registry/`):
- `ConfigSchemaReflector` — given a config Type, instantiates it (to read default values declared via property initializers), reads `[ConfigField]` attributes, normalises `TimeSpan` → `"hh:mm:ss"` and `decimal` → `double`, sorts by `(Group, Order, Name)`.
- `RegistryEntry` / `RegistryField` — DTOs returned by the API.
- `IScreenerRegistry` / `ScreenerRegistry` — 4 entries (`volumespike`, `breakout`, `dominancecandle`, `openingrange`); each routed through the reflector.
- `IStrategyRegistry` / `StrategyRegistry` — 4 entries with empty `fields[]` (strategies don't have their own configs today; framework's there to extend).

**Adding a new screener post-launch is now**: write the screener class + decorate its config + register one factory key + add one row to `ScreenerRegistry`. UI auto-renders the form. Zero front-end changes required.

**Verified:** `dotnet build` clean. Runtime check happens in Phase 4 smoke test (registry endpoint returned correct schema).

---

## Phase 4 — Web API surface

**NuGet** added to `DhanMarketData.Api`:
- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.7
- `Microsoft.AspNetCore.OpenApi` 10.0.7 *(not Swashbuckle — Microsoft's built-in is the .NET 10 default; `WithOpenApi()` is deprecated)*
- `Microsoft.Extensions.Configuration.Json` 10.0.7
- `System.Security.Cryptography.ProtectedData` 10.0.7 *(needed for DPAPI on .NET; not in BCL)*

**Engine touch-up** (additive — no logic re-ordering):
- `Backtesting/Engine/BacktestProgress.cs` — `BacktestEventKind` enum + `BacktestProgress` record.
- `Backtesting/Engine/BacktestOrchestrator.cs` — old `RunBacktestAsync(int?, int?)` retained as wrapper; new overload accepts `IProgress<BacktestProgress>` + `CancellationToken`. `cancellationToken.ThrowIfCancellationRequested()` added at chunk boundary, day-loop top, and stock-fetch loop top. `progress?.Report(...)` emitted at: start (Started), each chunk completion (ChunkProgress), each trade added (TradeRecorded), and end (Finished). The MaxTradesPerDay → candle-count → screen → entry → strategy → MaxCapitalPerTrade order is unchanged. `chunkSize = 30` unchanged.

**API contracts** (`Contracts/`):
- `StrategyContracts.cs` — `StrategyPresetSummaryDto`, `StrategyPresetDetailDto`, request DTOs for create/update/clone.
- `RunContracts.cs` — `StartRunRequest`, `BacktestRunSummaryDto` + `BacktestRunDetailDto` (inherits), `TradeRecordDto`, `TradeListDto`, `StartRunResponse`.
- `CredentialsContracts.cs` — `CredentialsStatusDto` (never returns plaintext token), `SetCredentialsRequest`.

**Services**:
- `ITokenProtector` / `DpapiTokenProtector` — Windows DPAPI under `DataProtectionScope.CurrentUser` with fixed entropy. Marked `[SupportedOSPlatform("windows")]`; `AssemblyInfo.cs` adds `[assembly: SupportedOSPlatform("windows")]` to silence CA1416 across the project.
- `IPresetExecutor` / `PresetExecutor` — given a preset row + `StartRunRequest`, builds a synthetic `IConfiguration` (with `Backtest`, `Trading`, and `Screeners:<key>` sections matching exactly what the existing `ScreenerFactory` / `StrategyFactory` expect) and constructs `BacktestEngine` + `BacktestOrchestrator`. **Factories are unmodified.**

**Hubs** (`Hubs/`):
- `BacktestHub` at `/hubs/backtest` — clients call `JoinRun(runId)` to subscribe to that run's group.
- `IBacktestHubBroadcaster` / impl — typed wrapper for the 6 server-pushed events: `RunStarted`, `ChunkProgress`, `TradeRecorded`, `RunCompleted`, `RunFailed`, `RunCancelled`.

**Background runner** (`BackgroundServices/`):
- `RunRequest` — small payload pushed onto the queue.
- `IBacktestRunQueue` / `BacktestRunQueue` — bounded `Channel<RunRequest>` (capacity 10), plus a `ConcurrentDictionary<int, CancellationTokenSource>` so DELETE /api/runs/{id} can signal in-flight cancellation.
- `BacktestRunner` (`IHostedService`) — single-consumer loop. Per run: creates a DI scope (own DbContext), flips status `Queued → Running`, registers a linked CTS with the queue, drains orchestrator events from a `ChannelProgress<BacktestProgress>` adapter, persists each trade as a `TradeRecord` row, broadcasts each event via SignalR, ends with `Completed | Failed | Cancelled`.

**Controllers** (`Controllers/`):
- `StrategiesController` — list / get / create / update / delete / **reset** (re-applies seed values for built-ins via `BuiltInPresets.All()`) / **clone**. Built-in lock enforced (cannot delete or update; clone or reset only).
- `RegistryController` — `GET /api/registry/screeners` and `/strategies` plus `/screeners/{key}` and `/strategies/{key}`.
- `CredentialsController` — `GET` returns `{clientId, hasToken, updatedAt}` (never plaintext); `PUT` accepts `{clientId, accessToken}`, encrypts via DPAPI, upserts row 1.
- `RunsController` — POST start (queues + 202), DELETE cancel (idempotent, distinguishes Queued vs Running), GET list/detail/trades (paged), `GET /{id}/csv` streams CSV in legacy column order (`Date,Symbol,EntryTime,EntryPrice,Quantity,StopLoss,Target,ExitTime,ExitPrice,ExitReason,PnL,PnL%`).

**`Program.cs`** — DI wiring: DbContext (SQLite), 4 scoped repositories, singleton registries + `BacktestRunQueue` + `IBacktestHubBroadcaster` + `ITokenProtector`, scoped `IPresetExecutor`, hosted `BacktestRunner`, controllers, SignalR, OpenAPI, CORS for `http://localhost:5173`. `WebHost.ConfigureKestrel(opt => opt.ListenLocalhost(5000))` — bound to `127.0.0.1:5000` only, not LAN-reachable. Startup tasks: `db.Database.Migrate()` and `runs.ResetOrphanedRunsAsync()` (marks any `Running`/`Cancelling` rows from a previous process as `Failed`).

**`appsettings.json`** — minimal: SQLite connection string (`Data Source=dhanmarketdata.db` — runtime DB at solution root), CORS allowed origins, log levels.

**Persistence touch-up:** `BuiltInPresets` made `public` so the API's reset endpoint can use it.

**Smoke-tested end-to-end** (API started on 127.0.0.1:5000):

| Endpoint | Result |
|---|---|
| `GET /api/strategies` | All 4 seeded built-in presets returned |
| `GET /api/registry/screeners` | DominanceCandle entry has 13 fields with correct kind (percent/multiplier/timeofday), label, default value, group |
| `GET /api/credentials` | `{"clientId":"","hasToken":false,"updatedAt":null}` (no token yet) |
| `GET /openapi/v1.json` | All 14 paths documented |
| Migrate-on-startup | DB created automatically; idempotent |

---

## What was verified end-to-end

- ✅ Full solution builds clean: 0 warnings, 0 errors across all 6 projects (Core, Infrastructure, Backtesting, Persistence, Api, Console).
- ✅ EF Core migration creates DB with 4 tables, correct indexes (unique `Name`, composite `(BacktestRunId, Date)`, etc.), seed data loaded.
- ✅ SQLite WAL mode active (EF Core default — no explicit `PRAGMA` needed).
- ✅ API process starts cleanly, applies migrations, resets orphan runs, binds 127.0.0.1:5000 only.
- ✅ Registry reflection emits correct field schema for all 4 screener configs.
- ✅ Strategies list endpoint returns the 4 seed rows.
- ✅ OpenAPI 3.1 spec served at `/openapi/v1.json` (Microsoft.AspNetCore.OpenApi, not Swashbuckle).
- ✅ Console smoke-test path still compiles (old `RunBacktestAsync(int?, int?)` signature retained).

## What was NOT verified

- ❌ End-to-end backtest run via API — would need a valid Dhan token (the one in `appsettings.local.json` is expired) and would touch the full screening/entry/exit logic. Recommended for Phase 5.5 once a token is refreshed.
- ❌ SignalR client roundtrip — needs a connected client. Phase 5's React UI provides that.
- ❌ Cancellation graceful exit, run failure recovery, orphan-run cleanup on real crash — code paths exist but only validated by static reasoning.
- ❌ CSV byte-equality vs legacy console output — Phase 0 baselines were skipped, so this is no longer a regression contract; first runs through the API will become the new contract.

---

## Phase 5 — React UI

Local-first SPA at `ui/`. Single-page tab switcher (no router) — Strategies / Run / Results / Credentials.

**Stack chosen for MVP:**
- Vite 6 + React 19 + TypeScript (strict)
- Tailwind v4 via `@tailwindcss/vite` (no v3-style `tailwind.config.js`, no postcss config)
- TanStack Query v5 — server state + 5s polling on the runs list
- `@microsoft/signalr` — live run progress

**Deliberate MVP skips** (all noted for post-MVP add):
- Router — replaced by tab switcher in `App.tsx`
- shadcn `init` — minimal Tailwind components written inline; tonal palette is zinc + emerald accent
- `openapi-typescript` — types in `src/types.ts` are hand-written from the C# DTOs
- `react-hook-form` + Zod — controlled inputs for MVP

**Files** (all under `ui/`):
- `package.json`, `vite.config.ts`, 3× `tsconfig*.json`, `index.html`, `index.css`
- `src/main.tsx` — `QueryClientProvider` shell
- `src/App.tsx` — tab switcher
- `src/types.ts` — API DTOs (mirror Api.Contracts)
- `src/lib/api.ts` — typed `fetch` wrapper with all endpoints
- `src/lib/signalr.ts` — `useBacktestProgress(runId)` hook merges all 6 hub events into one state
- `src/components/DynamicConfigForm.tsx` — registry-schema-driven form, groups by `[ConfigField(Group=...)]`
- `src/pages/StrategiesPage.tsx` — list + click-to-edit, Reset/Clone buttons for built-ins
- `src/pages/RunPage.tsx` — preset picker + Start/Cancel + live progress bar + streaming trade table
- `src/pages/ResultsPage.tsx` — past-runs table (5 s polling) + selected run's trades + CSV download link
- `src/pages/CredentialsPage.tsx` — Dhan client ID + token (server encrypts via DPAPI)

**Dev wiring:**
- Vite dev on `localhost:5173`, API on `127.0.0.1:5000`
- Vite proxy: `/api`, `/hubs` (with `ws: true`), `/openapi` → API
- API CORS already allow-lists `http://localhost:5173`

**Build verified:** `npm install` (102 packages, 21 s); `npm run build` → `tsc -b` clean, Vite output 312 kB JS / 14 kB CSS in 1.04 s.

---

## Phase 5.5 — Integration smoke test

**What was tested:** the runtime contract between the React UI dev server, the Vite proxy, and the running ASP.NET Core API.

**Procedure:** Started both servers (`dotnet run --project src/DhanMarketData.Api` on `127.0.0.1:5000` and `npm run dev` in `ui/` on `localhost:5173`), then issued requests to the Vite dev server URL (so requests flow through the proxy → API → response back).

**Results — all endpoints reachable through the proxy:**

| Test | Result |
|---|---|
| `GET http://localhost:5173/` (SPA shell) | 200, `<title>Dhan Market — Backtest Console</title>` returned |
| `GET /api/strategies` (via proxy) | 4 built-in presets returned |
| `GET /api/registry/screeners` (via proxy) | volumespike 3 fields · breakout 4 · dominancecandle 13 · openingrange 12 |
| `GET /openapi/v1.json` (via proxy) | 14 paths documented |
| Migrate-on-startup | DB created idempotently |
| API binds 127.0.0.1 only | confirmed (LAN unreachable) |
| Vite binds localhost (default) | IPv6 `::1` and IPv4 `localhost` resolved |

**Not tested (out of scope without a fresh Dhan token):**
- Triggering a real backtest run via `POST /api/runs`. The token in `appsettings.local.json` is expired (JWT `exp` Feb 2026; today is May 3 2026). Cache covers Mar 2022 → Feb 12 2026; any 50-day backtest from "today" would partially hit the API and fail at first cache miss.
- Live SignalR event stream end-to-end (negotiate works; events validated only by static reasoning).
- Cancellation / orphan-run recovery.
- CSV byte-equality vs legacy console output (Phase 0 baselines were skipped).

**To unblock the deferred tests:** refresh the Dhan token via `PUT /api/credentials`, kick off a 30-day backtest against the existing cache (so most data is cache-hit), watch SignalR events, compare CSV with a corresponding console run.

---

## Next session

**Phase 5 — React UI** ✅ done. Remaining:
- Vite + React 19 + TypeScript scaffold under `ui/`
- Tailwind v4 + shadcn/ui setup
- Add: TanStack Query, TanStack Router, react-hook-form + Zod, openapi-typescript, @microsoft/signalr
- 4 pages: Strategies, Run, Results, Credentials
- One reusable `DynamicConfigForm` driven by the registry schema endpoint
- Vite proxy for `/api/*` and `/hubs/*` → `http://127.0.0.1:5000`

Estimated 40–60 tool calls. Recommend starting with a fresh session budget.

**Phase 5.5 — Regression checkpoint**: trigger each of the 4 built-in presets via the API, compare CSV exports against console output (with a fresh Dhan token).

**Phase 6 — Cleanup**: delete `DhanMarketData.Console`, move root-level MD files into `docs/`, refresh content per the discrepancy list in `RESTRUCTURE_PLAN.md`, write a top-level `README.md`.

---

## Files created during Phases 2–4 (uncommitted)

```
src/DhanMarketData.Persistence/
├── AppDbContext.cs
├── DesignTimeDbContextFactory.cs
├── DhanMarketData.Persistence.csproj  [+ EF Core packages]
├── Entities/                          (4 files)
├── Migrations/                        (3 files — 20260502183443_InitialCreate)
├── Repositories/                      (8 files — 4 interfaces + 4 impls)
└── Seeding/BuiltInPresets.cs

src/DhanMarketData.Core/Configs/
├── BacktestConfig.cs                  (decorated)
├── ScreenerConfigs.cs                 (decorated)
└── Attributes/ConfigFieldAttribute.cs (NEW)

src/DhanMarketData.Backtesting/
├── Engine/BacktestProgress.cs         (NEW)
├── Engine/BacktestOrchestrator.cs     (additive overload only)
└── Registry/                          (6 files)

src/DhanMarketData.Api/
├── DhanMarketData.Api.csproj          [+ EF Core, OpenAPI, ProtectedData packages]
├── AssemblyInfo.cs                    [assembly: SupportedOSPlatform("windows")]
├── Program.cs                         (replaces placeholder)
├── appsettings.json                   (NEW)
├── Properties/launchSettings.json     (NEW — 127.0.0.1:5000, workdir = solution root)
├── Contracts/                         (3 files)
├── Services/                          (4 files — token protector + preset executor)
├── Hubs/                              (3 files — hub + broadcaster)
├── BackgroundServices/                (4 files — request, queue, runner)
└── Controllers/                       (4 files)

.gitignore                             (extended for *.db, *.db-shm, *.db-wal)
```

Tooling installed: `dotnet-ef` 10.0.7 globally.
