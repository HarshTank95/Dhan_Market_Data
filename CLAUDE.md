# Project orientation for Claude

Local web app that backtests intraday trading strategies on the Indian stock market (NSE) using historical candles from the Dhan v2 API. **ASP.NET Core 10 Web API + React 19 SPA + SQLite.** Single user, localhost only, no auth.

This file auto-loads. For deeper context, read in this order:

1. `README.md` — quick start
2. `docs/architecture.md` — 5-project solution layout + runtime flow
3. `docs/strategies.md` — the 4 built-in strategies and their actual rules
4. `docs/extending.md` — how to add a new screener or strategy
5. `docs/data-fetching.md` — Dhan API constraints, cache layout, token storage
6. `RESTRUCTURE_CHANGELOG.md` — what was migrated from console → web (Phases 1–6)

## Current state

Migration **complete** (Phases 1–6 all pushed). 5 .NET projects under `src/` + a Vite/React UI in `ui/`. Solution builds clean (0 warnings, 0 errors). API + UI integration smoke-tested through the Vite proxy. **Only thing not yet validated end-to-end: a real backtest run** — needs a fresh Dhan token (the one in `appsettings.local.json` is expired; new tokens go via the UI's Credentials page → `/api/credentials`, encrypted at rest with Windows DPAPI).

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

## Tech versions (locked in)

.NET 10 (RC2 SDK at time of migration) · EF Core 10.0.7 · React 19 · Vite 6 · TypeScript 5.7 · Tailwind v4 · TanStack Query v5 · @microsoft/signalr 9 · SQLite ≥ 3.46.1.

## Repo conventions

- **Memory file**: don't store implementation details in `~/.claude/projects/.../memory/` — they belong in code or in `docs/`. Memory is for cross-conversation user preferences.
- **Commits**: behavior-preservation rule means new commits should explicitly call out *what* (if anything) was a logic change versus an additive/decorative one.
- **`appsettings.local.json` is gitignored** and may contain a Dhan token. Don't read it back to the user verbatim.
