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

Stored encrypted at rest in SQLite (`ApiCredentials` row). The access token, plus the optional Pin and TOTP seed, are each encrypted with Windows DPAPI under `DataProtectionScope.CurrentUser` — only the same Windows user account on the same machine can decrypt. Secrets are write-only over the API (never returned). The legacy `appsettings.local.json` path is **no longer used**.

Three ways to set the active token, all via the **Credentials** page (or the `/api/credentials*` endpoints):

1. **Generate (TOTP)** — `POST /api/credentials/generate` with `clientId` + `pin` + `totp` (the current 6-digit authenticator code). Calls Dhan's `POST https://auth.dhan.co/app/generateAccessToken` (params on the **query string** — confirmed shape) and saves the returned token. Mints a token even when the current one is expired. *(Optionally, a stored base32 TOTP seed lets the app generate the 6-digit code itself via RFC 6238 — most users don't have the seed and just type the code.)*
2. **Renew** — same endpoint with no `totp`. Calls `GET https://api.dhan.co/v2/RenewToken` (current token in the `access-token` header) to roll an **active** token forward ~24h with no secrets. Fails if the token is already expired (Dhan rejects it) — then fall back to Generate.
3. **Paste** — `PUT /api/credentials` with a token you generated yourself on Dhan web.

Field-name gotchas (the two Dhan auth endpoints are inconsistent): **RenewToken returns the JWT in `token`**, while generateAccessToken's doc says `accessToken` — `DhanAuthClient.ParseTokenResponse` accepts either. Dhan also signals failures (e.g. `Invalid TOTP`) with `{ "status":"error", "message":... }` on an HTTP 200, surfaced as a clean error. All of this lives in `DhanMarketData.Infrastructure/Auth/` (`DhanAuthClient`, `TotpGenerator`, `JwtHelper`); orchestration is `TokenGenerationService` (Api).

Token expiry is read from the JWT's `exp` claim (`JwtHelper`) and cached in `ApiCredentials.TokenExpiresAt` for the UI status line. A generated/renewed token is valid ~24h. Surface auth errors when they happen rather than relying on a hardcoded TTL.

## Errors

`DhanDataApiClient` swallows `DH-905` errors (no data for a delisted/suspended stock) and returns an empty list. This was empirical behaviour in the legacy code; preserved verbatim.

Other errors propagate; the API surfaces them as `Failed` runs with `ErrorMessage` populated, plus a `RunFailed` SignalR event so the UI can show a toast.

## Two operating modes (legacy `BacktestConfig.DataFetchOnly`)

The legacy console supported a "fetch-and-cache only" mode that pre-warmed the cache without running a backtest. The new API path doesn't expose this directly — it's still supported in `BacktestOrchestrator` but no controller surfaces it. To pre-warm a cache, run a normal backtest with cheap configs; cached data is reused on the next run.
