# Data fetching

Historical candle data comes from the [Dhan v2 historical-data API](https://dhanhq.co/docs/v2/historical-data/). The app caches everything to disk so backtests are reproducible offline once the cache is warm.

## Cache layout

```
data/
└── NSE_EQ/                            # ExchangeSegment
    ├── 5min/
    │   └── {SecurityId}/
    │       ├── 2024-01-15.json
    │       ├── 2024-01-16.json
    │       └── …
    ├── 15min/
    ├── 60min/
    └── 1day/
```

Three layers in `HistoricalDataCache`:
- **In-memory LRU** — recent files, capped at 500
- **Disk** — JSON file per `(security, date)`
- **Negative cache** — `(security, date)` pairs known to have no data, to avoid re-fetching delisted/suspended stocks

## Supported timeframes

Per Dhan v2 docs (verified): minute intervals `1`, `5`, `15`, `25`, `60`, plus daily.

| App key | Dhan API param |
|---|---|
| `1min` | `1` |
| `5min` | `5` |
| `15min` | `15` |
| `25min` | `25` |
| `60min` (or `1hour`) | `60` |
| `1day` | `D` |

**4-hour candles are not supported by the Dhan API.** Use `60min`. The `HistoricalDataCache` switch throws on unsupported timeframes.

## Per-call constraint (important)

Dhan v2 docs: *"only 90 days of data can be polled at once for any of the above time intervals"* (intraday only). Daily candles have no cap.

The current orchestrator fetches one trading day per API call, so this is never breached. Any future bulk-fetch optimisation must respect the 90-day window.

## Rate limiting

`DhanDataApiClient` throttles requests with a 250 ms delay between calls (≈4 req/s). The exact public rate-limit number isn't documented in the current Dhan v2 docs (the rate-limit page returns 404 as of this writing) — the 250 ms throttle is empirical from the legacy code and should be left alone.

## Tokens

Stored encrypted at rest in SQLite (`ApiCredentials` row, `AccessTokenEncrypted` column). Encryption is Windows DPAPI under `DataProtectionScope.CurrentUser` — only the same Windows user account on the same machine can decrypt.

Set the token via the **Credentials** page in the UI (or `PUT /api/credentials`). The legacy `appsettings.local.json` path is **no longer used** by the API.

Token expiry: Dhan tokens have a short TTL (the legacy notes say ~5 days, but this isn't in the current public Dhan v2 docs). Surface auth errors when they happen rather than relying on a hardcoded TTL.

## Errors

`DhanDataApiClient` swallows `DH-905` errors (no data for a delisted/suspended stock) and returns an empty list. This was empirical behaviour in the legacy code; preserved verbatim.

Other errors propagate; the API surfaces them as `Failed` runs with `ErrorMessage` populated, plus a `RunFailed` SignalR event so the UI can show a toast.

## Two operating modes (legacy `BacktestConfig.DataFetchOnly`)

The legacy console supported a "fetch-and-cache only" mode that pre-warmed the cache without running a backtest. The new API path doesn't expose this directly — it's still supported in `BacktestOrchestrator` but no controller surfaces it. To pre-warm a cache, run a normal backtest with cheap configs; cached data is reused on the next run.
