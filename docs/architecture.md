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
│       ├── Program.cs                  # DI, EF migrate-on-startup, OpenAPI, SignalR, CORS
│       ├── Properties/launchSettings.json   # workdir = solution root
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
        └── pages/                      Strategies, Run, Results, Credentials
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
5. `BacktestOrchestrator.RunBacktestAsync(...)` runs with `IProgress<BacktestProgress>` + `CancellationToken`. Per chunk (30 days):
   - cache lookup → Dhan API on miss
   - screen each stock → produce signal candles
   - strategy `ExecuteTrade` → `Trade` if conditions met
   - emit `TradeRecorded` progress event
6. Runner drains progress events serially → writes `TradeRecord` rows + broadcasts SignalR events.
7. Final status: `Completed | Failed | Cancelled`.

## Behavior preservation guarantee

The screening and entry/exit logic from the legacy console is preserved byte-for-byte. The only engine change in the migration was an additive `IProgress<>` + `CancellationToken` parameter on `BacktestOrchestrator.RunBacktestAsync`. No method-body edits, no reordering, no default changes. See `RESTRUCTURE_CHANGELOG.md` for the audit trail.
