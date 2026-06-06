# Diagnostic decision log

An opt-in, per-run audit trail that records **why every screened stock was kept
or dropped** on each trading day — the screening funnel, not just the trades that
fired. Built to validate a strategy before trusting it in a live engine: you can
cross-check, per stock and per day, exactly which filter eliminated it and at what
price.

## Turning it on

Per run, from the **Run** tab: tick **"Enable detailed log"** before *Start run*.
It is **off by default** — the log is large (a 500-day, 500-stock run evaluates
~250k stock-days → multi-MB file), so only enable it when you're auditing.

Under the hood the flag rides on `StartRunRequest.EnableDiagnosticLog` →
`BacktestRun.DiagnosticLogEnabled`. When set, the runner opens a streaming writer
for the run and threads it through `PresetExecutor` → `BacktestOrchestrator`.

## Where it lives

One file per run: `logs/run-{id}.jsonl`, under the solution root (next to
`data/`; the CWD is anchored there in `Program.cs`). Format is **JSONL** — one
compact JSON object per line, streamed to disk as the backtest runs (never
buffered in memory). Managed by `BacktestLogStore` (`DhanMarketData.Api`).

## What each line looks like

One decision per `(stock, day)` evaluation. Null fields are omitted.

```jsonl
{"symbol":"FSL","securityId":"14366","date":"2026-06-02T00:00:00","outcome":"traded","price":270.80,"entryTime":"2026-06-02T05:00:00","entryPrice":270.80,"quantity":18,"stopLoss":268.10,"target":274.50,"exitTime":"2026-06-02T05:50:00","exitPrice":273.50,"exitReason":"Target Hit","pnl":672.51,"pnlPercent":1.00}
{"symbol":"INFY","securityId":"1594","date":"2026-06-02T00:00:00","outcome":"rejected","stage":"gap_atr_ratio","detail":"gap/ATR=3.21 outside [0.50, 2.50]","price":1402.30}
{"symbol":"TCS","securityId":"11536","date":"2026-06-02T00:00:00","outcome":"rejected","stage":"vwap_slope_low","detail":"VWAP slope 12.4 bps < min 20.0","price":3890.10}
{"symbol":"WIPRO","securityId":"3787","date":"2026-06-02T00:00:00","outcome":"no_data"}
```

### `outcome` values

| outcome | meaning |
|---|---|
| `no_data` | no candles cached for that stock-day |
| `insufficient_candles` | <4 candles cached (can't simulate) |
| `day_skipped_regime` | whole day skipped by the regime breaker (one `*` row per day) |
| `rejected` | a screener filter dropped it — see `stage` + `detail` |
| `screened_no_entry` | passed the screen but no candle at the entry time |
| `no_signal` | screened in with an entry candle, but the strategy produced no trade |
| `skipped_capital` | a trade formed but exceeded `MaxCapitalPerTrade` |
| `traded` | a trade was taken — full lifecycle fields populated |

`stage` is a short slug (e.g. `liquidity`, `gap_not_down`, `rvol_too_low`),
`detail` is the human-readable reason with the actual values, and `price` is the
price at the decision point (entry-candidate open, breakout close, …).

### Flat-chain vs scan-loop screeners

- **Gap Fade** is a flat filter chain, so `stage`/`detail` pinpoint the exact gate
  that dropped the stock.
- **EMA Gap-Down Reclaim**, **VWAP ORB**, and **Volume Confluence** scan candles
  in a loop where a single `continue` isn't terminal. They record the *furthest*
  stage any candle reached, so a `rejected` row reads like "broke the OR high but
  never held a rising VWAP" — telling you how close the stock got to triggering.

## Managing the files

- **Download** — Results tab → select a run → **Download log** (`GET /api/runs/{id}/log`).
- **Delete one** — Results tab → **Delete log** (`DELETE /api/runs/{id}/log`).
- **Delete all** — Results tab → **Delete all logs** (`DELETE /api/runs/logs`).

Run summaries expose `hasDiagnosticLog` (file present on disk) and
`diagnosticLogEnabled` (was requested at start) so the UI shows the controls only
when there's a log to act on.

## Design note (behavior preservation)

The whole feature is a **pure side-channel**, the same discipline as the existing
`IProgress<BacktestProgress>` hook:

- A `ScreenDecisionRecorder` rides on `ScreenerContext.Decisions` (an `init`
  property, null unless logging is on). Screeners call `Decisions?.Reject(...)` /
  `Decisions?.Note(...)` at their existing drop sites — these never change the
  boolean result or the filter order.
- `BacktestEngine.BacktestDayWithDecision` mirrors `BacktestDay` through one
  shared core; when no recorder is passed the path is **byte-identical** to before.
- The orchestrator only writes decisions when a writer is supplied; with logging
  off it calls the original `BacktestDay` and behaves exactly as it always did.

So enabling the log cannot change which trades a backtest produces — it only
observes the decisions already being made.
