# Commands reference

Everything you need to build, run, and maintain the app. Run all commands from the **repo root** (`D:\Code\C_Sharp\6_Dhan_Market_Data`) unless noted. Shell is **PowerShell**.

## Daily workflow — start the app

Two terminals, both from the repo root:

```powershell
# Terminal 1 — API on http://127.0.0.1:5000
dotnet run --project src/DhanMarketData.Api
```

```powershell
# Terminal 2 — Vite dev server on http://localhost:5173 (with /api + /hubs proxy)
cd ui
npm run dev
```

Then open **http://localhost:5173** in your browser.

To stop either: focus its terminal and press `Ctrl+C`.

## First-time setup

Run these once after cloning (or after deleting `bin/`, `obj/`, or `node_modules/`):

```powershell
# Restore + build the .NET solution
dotnet restore
dotnet build

# Install UI dependencies
cd ui
npm install
cd ..
```

Then start the app via the daily-workflow commands above. On first launch, the API:
1. Creates `dhanmarketdata.db` at the repo root
2. Applies EF Core migrations
3. Seeds the 4 built-in strategy presets

After that, open the UI → **Credentials** tab → paste your Dhan client ID + access token (stored encrypted via Windows DPAPI).

## Build

```powershell
# Build everything
dotnet build

# Build only the API (and its dependencies)
dotnet build src/DhanMarketData.Api

# Clean rebuild
dotnet clean
dotnet build

# UI production bundle (output goes to ui/dist/)
cd ui
npm run build

# UI type-check only (no emit)
cd ui
npx tsc --noEmit
```

## Run

```powershell
# API (default — Development env, http://127.0.0.1:5000)
dotnet run --project src/DhanMarketData.Api

# API in Release mode
dotnet run --project src/DhanMarketData.Api --configuration Release

# UI dev server (HMR, proxies /api + /hubs to 127.0.0.1:5000)
cd ui
npm run dev

# Preview the production UI bundle (after npm run build)
cd ui
npm run preview
```

## EF Core — database migrations

The Persistence project is the migrations target; the API is the startup project.

```powershell
# Add a new migration
dotnet ef migrations add <MigrationName> `
  --project src/DhanMarketData.Persistence `
  --startup-project src/DhanMarketData.Api

# Apply pending migrations (also runs automatically at API startup)
dotnet ef database update `
  --project src/DhanMarketData.Persistence `
  --startup-project src/DhanMarketData.Api

# Roll back the last migration (specify the previous migration name)
dotnet ef database update <PreviousMigrationName> `
  --project src/DhanMarketData.Persistence `
  --startup-project src/DhanMarketData.Api

# Remove the most recent unapplied migration
dotnet ef migrations remove `
  --project src/DhanMarketData.Persistence `
  --startup-project src/DhanMarketData.Api

# List all migrations
dotnet ef migrations list `
  --project src/DhanMarketData.Persistence `
  --startup-project src/DhanMarketData.Api
```

Install the EF tool once (machine-wide) if missing:
```powershell
dotnet tool install --global dotnet-ef
dotnet tool update --global dotnet-ef
```

## Reset / clean state

```powershell
# Wipe runtime DB — re-seeds the 4 built-in presets on next API start (you'll re-enter the Dhan token)
Remove-Item dhanmarketdata.db, dhanmarketdata.db-shm, dhanmarketdata.db-wal -ErrorAction SilentlyContinue

# Wipe the historical-candle cache
Remove-Item data\NSE_EQ -Recurse -Force

# Clean .NET build outputs
dotnet clean
Remove-Item src\*\bin, src\*\obj -Recurse -Force -ErrorAction SilentlyContinue

# Clean UI build outputs
Remove-Item ui\node_modules, ui\dist -Recurse -Force -ErrorAction SilentlyContinue
```

## Diagnostics

```powershell
# Check what's holding port 5000 or 5173
Get-NetTCPConnection -LocalPort 5000 -State Listen | Select-Object OwningProcess
Get-Process -Id <PID>

# Kill a stuck API process (only when sure no work in flight)
Stop-Process -Id <PID>

# Confirm tool versions
dotnet --version          # expect .NET 10.x SDK
node --version            # expect 20+
npm --version
dotnet ef --version
```

## API surface (sanity checks)

With the API running:

```powershell
# OpenAPI spec
Invoke-RestMethod http://127.0.0.1:5000/openapi/v1.json | ConvertTo-Json -Depth 4

# List strategy presets
Invoke-RestMethod http://127.0.0.1:5000/api/strategies

# List backtest runs
Invoke-RestMethod http://127.0.0.1:5000/api/runs

# Cancel a single run (idempotent — no-op for already-terminal rows)
Invoke-RestMethod -Method Delete http://127.0.0.1:5000/api/runs/<id>

# Cancel every active run (Queued / Running / Cancelling) — same as Queue tab "Stop all"
Invoke-RestMethod -Method Post http://127.0.0.1:5000/api/runs/cancel-active

# Force-finalise orphan rows without restarting the API.
# Walks Queued + Running + Cancelling, skips rows the runner is actually working,
# flips the rest to Cancelled. Returns { cleanedCount }.
Invoke-RestMethod -Method Post http://127.0.0.1:5000/api/runs/cleanup-orphans
```

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `instruments.csv not found. Place it in project root.` | API CWD didn't anchor to repo root | Make sure you're running the latest `Program.cs` (the CWD-anchor block is right after `WebApplication.CreateBuilder`). Restart the API. |
| `dhanmarketdata.db` appears under `src/DhanMarketData.Api/` | Same as above — CWD wasn't anchored | Stop API. `Move-Item src\DhanMarketData.Api\dhanmarketdata.db* .\` Restart. |
| Build fails with `MSB3027 ... file is locked by DhanMarketData.Api (PID …)` | The API is still running and holding `bin\…\.exe` | `Ctrl+C` the API terminal, then rebuild. |
| Browser can't reach `http://127.0.0.1:5173` but `http://localhost:5173` works | Vite binds to IPv6 `localhost`, not `127.0.0.1` | Use `localhost:5173`. |
| `dotnet ef` not found | EF tool not installed | `dotnet tool install --global dotnet-ef` |
| UI shows CORS / network errors | API not running, or running on the wrong port | Confirm Terminal 1 shows `Now listening on: http://127.0.0.1:5000`. |
| Backtest fails with auth error from Dhan | Token expired (Dhan tokens are short-lived) | UI → Credentials tab → paste a fresh token. |
| Queue tab shows rows stuck in `Cancelling` | DELETE was issued against a run that had no in-flight runner (e.g. API restarted between enqueue and cancel) | Restart the API — `ResetOrphanedRunsAsync` flips them to `Failed` on startup (look for `Reset N orphaned run(s)` log line). Or, if you don't want to restart: `Invoke-RestMethod -Method Post http://127.0.0.1:5000/api/runs/cleanup-orphans`. |
| Run sits at `Queued` forever | Single-consumer runner is busy with an earlier long run (cold-cache fetches can take minutes) | Check the Queue tab — every active run is visible there. Use per-row Stop or "Stop all" if you need to clear the head. |
| API logs `Reset 0 orphaned run(s)` when you expected non-zero | The DB the API is using is not the one with the orphan rows. | Confirm `dhanmarketdata.db` is at the repo root (not under `src/DhanMarketData.Api/`). The Phase 7 CWD-anchor block in `Program.cs` should keep this from happening. |

## File locations (reference)

| Path | What |
|---|---|
| `dhanmarketdata.db` (repo root) | Runtime SQLite database |
| `data/NSE_EQ/<timeframe>/<securityId>/<date>.json` | Historical candle cache |
| `instruments.csv` (repo root) | NSE instrument master, loaded at API startup |
| `src/DhanMarketData.Api/appsettings.json` | API logging + CORS + DB connection string |
| `appsettings.local.json` (repo root, gitignored, **legacy**) | Old console token file — no longer read by anything |
| `src/DhanMarketData.Persistence/Migrations/` | EF Core migration history |
