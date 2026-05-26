# Project orientation for Claude

Local web app that backtests intraday trading strategies on the Indian stock market (NSE) using historical candles from the Dhan v2 API. **ASP.NET Core 10 Web API + React 19 SPA + SQLite.** Single user, localhost only, no auth.

This file auto-loads. For deeper context, read in this order:

1. `README.md` — quick start
2. `docs/architecture.md` — 5-project solution layout + runtime flow
3. `docs/strategies.md` — the 7 built-in strategies and their actual rules
4. `docs/extending.md` — how to add a new screener or strategy
5. `docs/data-fetching.md` — Dhan API constraints, cache layout, token storage
6. `RESTRUCTURE_CHANGELOG.md` — what was migrated from console → web (Phases 1–6)
7. `docs/strategy-optimization-playbook.md` — reusable, criteria-driven method to take *any* strategy from idea/loss to a robust positive-expectancy preset (measure-don't-guess loop, robustness gates, lock-in checklist). Hand it to Claude with a hypothesis + success criteria.

## Current state

Migration **complete** (Phases 1–6 all pushed). 5 .NET projects under `src/` + a Vite/React UI in `ui/`. Solution builds clean (0 warnings, 0 errors). Backtests run end-to-end against the Dhan v2 API (token set via the UI's Credentials page → `/api/credentials`, encrypted at rest with Windows DPAPI; tokens expire ~daily — refresh when fetches start failing or runs return 0 trades).

Newest strategy: **EMA Gap-Down Reclaim (Long)** (`emapullback`, preset #7) — a data-tuned buy-the-dip (gap-down ≥1.5% in a daily uptrend, 9-EMA reclaim trigger). Developed/validated in-app on 500 NSE stocks × 250 days (net +66k, PF ~1.8, 13/13 months positive, in-sample — paper-test pending). See `docs/strategies.md`.

## Run it

Two terminals from the repo root:

```bash
dotnet run --project src/DhanMarketData.Api    # API on 127.0.0.1:5000
cd ui && npm run dev                            # Vite on localhost:5173 with proxy
```

Open `http://localhost:5173` → set credentials → run a backtest.

## Gotchas (high-leverage things to know before changing code)

- **Behavior-preservation rule**: screener / strategy / engine logic was migrated *byte-for-byte* from the legacy console. Decoration with `[ConfigField]` was the only allowed touch on configs. The only allowed engine touch was adding `IProgress<BacktestProgress>` + `CancellationToken` to `BacktestOrchestrator`. Don't reorder the `MaxTradesPerDay → candle-count → screen → entry → strategy → MaxCapitalPerTrade` sequence. `chunkSize = 30` stays.
- **Dhan API quirks**: supports `1/5/15/25/60` min and `D` only — **no 4-hour candles**. Max 90 days per call for intraday. The 250 ms throttle in `DhanDataApiClient` is empirical (rate-limit page in v2 docs is 404).
- **EF Core SQLite WAL mode is the default for new DBs** — do *not* add a `PRAGMA journal_mode=WAL` call; `Journal Mode=WAL` in the connection string has known parser issues.
- **OpenAPI is `Microsoft.AspNetCore.OpenApi` (built-in)** — not Swashbuckle. `WithOpenApi()` is deprecated in .NET 10 (`ASPDEPR002`).
- **Token encryption is Windows DPAPI** (`ProtectedData.Protect`, `CurrentUser` scope) via `DpapiTokenProtector`. App is `[assembly: SupportedOSPlatform("windows")]`.
- **Tailwind v4 setup**: `@tailwindcss/vite` plugin only — no `tailwind.config.js`, no `postcss.config.js`. Theme via `@theme` block in `src/index.css`.
- **No router in the UI** — single-page tab switcher in `App.tsx` for MVP.
- **Types in `ui/src/types.ts` are hand-written** to mirror C# DTOs. When you change `Api.Contracts/*.cs`, mirror the change there. (Future: switch to `openapi-typescript` against `/openapi/v1.json`.)
- **Adding a new screener requires 5 touchpoints** — see `docs/extending.md`. Most-missed: `PresetExecutor.BuildConfiguration`'s screener-section-key switch.
- **Built-in strategy presets cannot be edited or deleted** (server enforces). UI shows Reset / Clone for built-ins, Save / Delete for user presets.
- **The legacy console (`src/DhanMarketData.Console`) was removed in Phase 6.** The API is the only host now. The legacy `appsettings.json` at the repo root is not read by anything; left as historical reference.
- **Working directory is anchored in `Program.cs`** (walks up from `AppContext.BaseDirectory` to find `DhanMarketData.sln`). `launchSettings.json`'s `workingDirectory = $(SolutionDir)` is a Visual Studio macro and is *empty* under `dotnet run` — without the anchor, `instruments.csv` doesn't load and the SQLite DB lands under `src/DhanMarketData.Api/`.
- **Cancel + orphan recovery (Phase 7).** The runner registers a `CancellationTokenSource` per in-flight run. `BacktestRunQueue.TryCancel(id)` returns `false` if there is no live CTS (e.g. API restarted between enqueue and cancel) — `RunsController.Cancel` checks this and force-finalises orphans to `Cancelled` instead of leaving them in `Cancelling`. Three cancel surfaces: `DELETE /api/runs/{id}`, `POST /api/runs/cancel-active` (bulk, used by the Queue tab "Stop all"), `POST /api/runs/cleanup-orphans` (manual safety net, no restart). Startup also runs `ResetOrphanedRunsAsync` which flips any `Queued`/`Running`/`Cancelling` rows from a previous process to `Failed` (logs `Reset N orphaned run(s)`).
- **`CancellationToken` is threaded through the HTTP/cache layer** (`HistoricalDataCache` → `DhanDataApiClient` → rate limiter, `Task.Delay`, `PostAsync`). Don't drop it on new code paths — without it a cancel during the cold-cache fetch only fires at the next chunk boundary, which can be minutes. The cache also re-throws `OperationCanceledException` instead of writing a missing-data marker.
- **`FetchProgress` is a separate SignalR event from `ChunkProgress`.** It fires per-stock during the cache warmup phase so the UI's progress bar moves before any chunk completes. `signalr.ts` weights it ~70% of the composite progress bar.
- **`activeRunId` lives in `localStorage`** (`dhan.activeRunId`) so the Run tab survives navigation away and back. Cleared on terminal status. The Run page hydrates state via `getRun + getRunTrades` after `JoinRun` to handle missed events during reconnect; trades are de-duped by `id`.
- **Queue tab is the operator console for running work.** It polls `/api/runs` every 1.5 s and shows every `Queued`/`Running`/`Cancelling` row; the Run tab only shows the currently-tracked run. If a user reports "stuck" rows, point them at the Queue tab first.

## Tech versions (locked in)

.NET 10 (RC2 SDK at time of migration) · EF Core 10.0.7 · React 19 · Vite 6 · TypeScript 5.7 · Tailwind v4 · TanStack Query v5 · @microsoft/signalr 9 · SQLite ≥ 3.46.1.

## Repo conventions

- **Memory file**: don't store implementation details in `~/.claude/projects/.../memory/` — they belong in code or in `docs/`. Memory is for cross-conversation user preferences.
- **Commits**: behavior-preservation rule means new commits should explicitly call out *what* (if anything) was a logic change versus an additive/decorative one.
- **`appsettings.local.json` is gitignored** and may contain a Dhan token. Don't read it back to the user verbatim.
