# Architecture

5-project .NET solution + 1 React app. UI calls API; API depends on Backtesting + Persistence + Infrastructure + Core. One-way dependencies, each library independently testable.

## Solution layout

```
6_Dhan_Market_Data/
├── DhanMarketData.sln
├── appsettings.json                    # legacy console config (kept as reference)
├── appsettings.local.json              # Dhan creds (gitignored) — superseded by /api/credentials
├── instruments.csv                     # NSE instrument list, loaded at runtime
├── data/                               # candle file cache (gitignored)
│   └── NSE_EQ/{timeframe}/{securityId}/{date}.json
├── backtest_results/                   # CSV exports (gitignored)
│
├── src/
│   ├── DhanMarketData.Core/            # shared domain
│   │   ├── Models/        Candle, Instrument, Trade
│   │   ├── Interfaces/    IScreener, IStrategy
│   │   └── Configs/       BacktestConfig, TradingConfig, ScreenerConfigs (4),
│   │                      Attributes/ConfigFieldAttribute
│   │
│   ├── DhanMarketData.Infrastructure/  # external IO
│   │   ├── Api/           DhanDataApiClient, DhanHistoricalResponse
│   │   ├── Caching/       HistoricalDataCache (memory LRU + disk + negative cache)
│   │   ├── Calendar/      TradingCalendarService (weekdays + hardcoded holidays)
│   │   ├── Instruments/   InstrumentService, Nifty500Stocks
│   │   └── Logging/       ErrorLogger
│   │
│   ├── DhanMarketData.Backtesting/     # screeners, strategies, engine, registry
│   │   ├── Screeners/     VolumeSpike, Breakout, DominanceCandle, OpeningRange
│   │   │                  + ScreenerFactory
│   │   ├── Strategies/    FixedTarget, BreakoutEntry, TrailingStop,
│   │   │                  OpeningRangeBreakout + StrategyFactory
│   │   ├── Engine/        BacktestEngine, BacktestOrchestrator, BacktestProgress
│   │   ├── Reports/       ReportService (CSV writer)
│   │   └── Registry/      IScreenerRegistry / IStrategyRegistry
│   │                      + ConfigSchemaReflector ([ConfigField] → JSON schema)
│   │
│   ├── DhanMarketData.Persistence/     # EF Core SQLite
│   │   ├── AppDbContext.cs
│   │   ├── DesignTimeDbContextFactory.cs
│   │   ├── Entities/      StrategyPreset, BacktestRun (RunStatus enum),
│   │   │                  TradeRecord, ApiCredentials
│   │   ├── Repositories/  4 thin interface + impl pairs
│   │   ├── Seeding/       BuiltInPresets (4 seed rows)
│   │   └── Migrations/
│   │
│   └── DhanMarketData.Api/             # web host
│       ├── Program.cs                  # DI, EF migrate-on-startup, OpenAPI, SignalR, CORS,
│       │                               # walks up to DhanMarketData.sln + SetCurrentDirectory
│       │                               # (workingDirectory in launchSettings is a VS-only macro)
│       ├── Properties/launchSettings.json   # workdir = solution root (VS only)
│       ├── AssemblyInfo.cs             # [assembly: SupportedOSPlatform("windows")]
│       ├── Contracts/     request + response DTOs (Strategy, Run, Credentials)
│       ├── Services/      ITokenProtector / DpapiTokenProtector,
│       │                  IPresetExecutor (preset JSON → IConfiguration → factories)
│       ├── Hubs/          BacktestHub + IBacktestHubBroadcaster
│       ├── BackgroundServices/  Channel<RunRequest> + BacktestRunner IHostedService
│       └── Controllers/   Strategies, Registry, Credentials, Runs
│
└── ui/                                 # React SPA (Vite + Tailwind v4)
    ├── package.json                    # React 19, TanStack Query, @microsoft/signalr
    ├── vite.config.ts                  # /api + /hubs (ws) + /openapi proxy → :5000
    └── src/
        ├── App.tsx                     # tab switcher (no router for MVP)
        ├── types.ts                    # hand-written API DTOs
        ├── lib/                        api.ts (typed fetch), signalr.ts (live progress hook)
        ├── components/                 DynamicConfigForm (registry-schema-driven)
        └── pages/                      Strategies, Run, Queue, Results, Credentials
```

## Dependency graph

```
ui/  ─────────────────────────────► Api (HTTP + SignalR)
                                     │
                                     ▼
                                 Persistence ─► Core
                                 Backtesting ─► Infrastructure ─► Core
                                                                  ▲
                                                                  │
                                                              Backtesting ─┘
```

## Runtime flow — a backtest run

1. UI `POST /api/runs` with `{ presetId, stockCount, backtestDays, timeframe }`.
2. `RunsController` writes a `BacktestRun` row (`Status = Queued`) and pushes a `RunRequest` onto the `Channel<RunRequest>`.
3. `BacktestRunner` (single-consumer `IHostedService`) dequeues, opens a DI scope, flips `Status → Running`.
4. Runner builds an `IPresetExecutor`, hands it the preset row + run params. The executor synthesises an `IConfiguration` from the preset's JSON columns and feeds it to the existing **unmodified** `ScreenerFactory` / `StrategyFactory`.
5. `BacktestOrchestrator.RunBacktestAsync(...)` runs with `IProgress<BacktestProgress>` + `CancellationToken`. The token is threaded all the way down through `HistoricalDataCache` → `DhanDataApiClient` (rate limiter, HTTP `PostAsync`, response read) so a cancel is observed within ~250 ms (one rate-limit slice), not at the next chunk boundary. Two phases:
   - **Fetch** — warm cache for every selected stock; emit `FetchProgress` per stock with `(symbol, stocksProcessed, totalStocks)`.
   - **Per chunk (30 days)** — screen each stock → produce signal candles → strategy `ExecuteTrade` → `Trade` if conditions met → emit `TradeRecorded`. End of chunk emits `ChunkProgress`.
6. Runner drains progress events serially → writes `TradeRecord` rows + broadcasts SignalR events.
7. Final status: `Completed | Failed | Cancelled`.

## SignalR events (`/hubs/backtest`)

Client calls `JoinRun(runId)` to subscribe to that run's group. Server-pushed events:

| Event | When |
|---|---|
| `RunStarted` | Runner flips `Status → Running` |
| `FetchProgress` | One per stock during the cold-cache warmup phase (`{symbol, stocksProcessed, totalStocks}`) |
| `ChunkProgress` | At each 30-day chunk boundary (`{currentChunk, totalChunks, daysProcessed, daysPlanned}`) |
| `TradeRecorded` | Each new `Trade` produced by the strategy (full trade payload) |
| `RunCompleted` / `RunFailed` / `RunCancelled` | Terminal — runner exits the per-run scope |

The UI hydrates state with `GET /api/runs/{id}` + `GET /api/runs/{id}/trades` after `JoinRun` so it's correct even if events were missed during reconnect.

## Queue + cancellation lifecycle

- `BacktestRunQueue` wraps a `Channel<RunRequest>` (capacity 10) plus a `ConcurrentDictionary<int, CancellationTokenSource>` of *in-flight* runs (the dictionary is keyed only while a runner is actively processing). Three lookups: `TryRegisterCancellation`, `TryCancel(id)` (returns `false` if there is no live CTS), and `HasInFlight(id)`.
- `DELETE /api/runs/{id}` (single cancel) — checks `TryCancel`'s return value: if there's no in-flight token, the row is force-finalised to `Cancelled` instead of left in `Cancelling` (otherwise no one would ever flip it).
- `POST /api/runs/cancel-active` — bulk cancel for the Queue page's "Stop all" button.
- `POST /api/runs/cleanup-orphans` — manual safety net. Walks every `Queued`/`Running`/`Cancelling` row, skips any with `HasInFlight(id) == true`, force-finalises the rest. Mirrors the startup orphan-reset but on demand.
- **Startup recovery** — `BacktestRunRepository.ResetOrphanedRunsAsync` on `Program.cs` startup flips any `Queued`/`Running`/`Cancelling` row from a previous process to `Failed`. Logs `Reset N orphaned run(s) from previous process.` so it's verifiable.

## UI tabs

| Tab | Purpose |
|---|---|
| Strategies | List/edit user presets, Reset/Clone built-ins (built-ins are read-only) |
| Run | Pick preset → set stock count + days + timeframe → Start → live progress (composite fetch + chunk bar). `activeRunId` persisted in `localStorage` so a tab switch doesn't lose the session. |
| Queue | All currently active runs (`Queued`/`Running`/`Cancelling`) with per-row Stop and bulk "Stop all". Polls `GET /api/runs` every 1.5 s. |
| Results | Past-runs table, selected run's trade list + P&L, CSV download. |
| Credentials | Set Dhan client ID + access token (server encrypts via DPAPI). |

## Behavior preservation guarantee

The screening and entry/exit logic from the legacy console is preserved byte-for-byte. The only engine change in the migration was an additive `IProgress<>` + `CancellationToken` parameter on `BacktestOrchestrator.RunBacktestAsync`; Phase 7 added a `FetchProgress` event kind and threaded the token through the HTTP/cache layer (additive only — no logic re-ordering, no default changes). See `RESTRUCTURE_CHANGELOG.md` for the audit trail.
