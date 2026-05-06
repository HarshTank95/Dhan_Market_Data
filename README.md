# Dhan Market Data — Backtesting Console

Local web app that backtests intraday trading strategies on the Indian stock market (NSE) using historical candles from the Dhan v2 API.

- **Backend** — ASP.NET Core 10 Web API (`src/DhanMarketData.Api`)
- **Frontend** — React 19 + Vite + Tailwind v4 (`ui/`)
- **Storage** — SQLite via EF Core (config presets, run history, trades)
- **Hosting** — `127.0.0.1:5000` (API) + `localhost:5173` (UI), single user, no auth

## Quick start

Two terminals from the repo root:

```bash
# 1. API (creates SQLite DB on first run, applies migrations, serves /api + /hubs + /openapi)
dotnet run --project src/DhanMarketData.Api

# 2. UI (Vite dev server with proxy to the API)
cd ui && npm install && npm run dev
```

Then open `http://localhost:5173` in a browser.

First-time setup: open the **Credentials** tab, paste your Dhan client ID + access token (encrypted at rest via Windows DPAPI), then drive a backtest from the **Run** tab. Active backtests show up in the **Queue** tab where you can stop a single run or cancel everything at once.

## Documentation

- `docs/commands.md` — every build/run/EF/cleanup command in one place
- `docs/architecture.md` — solution layout, dependency graph, where things live
- `docs/strategies.md` — the 4 built-in strategies, their actual screening rules, configs
- `docs/data-fetching.md` — caching, supported timeframes, Dhan API constraints
- `docs/extending.md` — how to add a new screener or execution strategy
- `RESTRUCTURE_PLAN.md` + `RESTRUCTURE_CHANGELOG.md` — migration history (console → web)

## Status

| Component | State |
|---|---|
| Backend (Phases 1–4) | ✅ working — smoke-tested |
| Frontend (Phase 5) | ✅ working — builds clean |
| API ↔ UI integration (Phase 5.5) | ✅ verified through Vite proxy |
| End-to-end backtest run | Needs a fresh Dhan token to validate |
