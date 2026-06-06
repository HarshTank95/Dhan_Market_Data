import { useEffect, useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'

import { api } from '../lib/api'
import { useBacktestProgress } from '../lib/signalr'

const ACTIVE_RUN_KEY = 'dhan.activeRunId'

function readStoredRunId(): number | null {
  try {
    const raw = localStorage.getItem(ACTIVE_RUN_KEY)
    if (!raw) return null
    const n = Number(raw)
    return Number.isFinite(n) && n > 0 ? n : null
  } catch {
    return null
  }
}

export function RunPage() {
  const presets = useQuery({ queryKey: ['strategies'], queryFn: api.listStrategies })
  const [presetId, setPresetId] = useState<number | null>(null)
  const [stockCount, setStockCount] = useState(500)
  const [backtestDays, setBacktestDays] = useState(50)
  const [timeframe, setTimeframe] = useState('5min')
  const [enableLog, setEnableLog] = useState(false)
  const [activeRunId, setActiveRunId] = useState<number | null>(() => readStoredRunId())

  const progress = useBacktestProgress(activeRunId)

  const start = useMutation({
    mutationFn: () => api.startRun({
      presetId: presetId!,
      stockCount,
      backtestDays,
      timeframe,
      exchangeSegment: 'NSE_EQ',
      enableDiagnosticLog: enableLog,
    }),
    onSuccess: r => {
      try { localStorage.setItem(ACTIVE_RUN_KEY, String(r.runId)) } catch {}
      setActiveRunId(r.runId)
    },
  })

  // Drop the persisted runId once the run reaches a terminal state — otherwise
  // the next page load would resurrect a run the user has already seen finish.
  useEffect(() => {
    const s = progress?.status
    if (s === 'completed' || s === 'failed' || s === 'cancelled') {
      try { localStorage.removeItem(ACTIVE_RUN_KEY) } catch {}
    }
  }, [progress?.status])

  const cancel = useMutation({
    mutationFn: () => api.cancelRun(activeRunId!),
  })

  const finished = progress?.status === 'completed' || progress?.status === 'failed' || progress?.status === 'cancelled'
  const active = activeRunId !== null && !finished

  // Composite progress: each chunk = 1.0 of progress; within a chunk, fetch ≈ 70%, backtest ≈ 30%.
  // This keeps the bar moving even before the first ChunkProgress event fires.
  const pct = (() => {
    if (!progress || progress.totalChunks === 0) return 0
    const completedChunks = progress.daysProcessed > 0 && progress.totalDaysPlanned > 0
      ? (progress.daysProcessed / progress.totalDaysPlanned) * progress.totalChunks
      : Math.max(0, progress.currentChunk - 1)
    const fetchPart = progress.fetch && progress.fetch.totalStocks > 0
      ? (progress.fetch.stocksProcessed / progress.fetch.totalStocks) * 0.7
      : 0
    const fraction = (completedChunks + fetchPart) / progress.totalChunks
    return Math.min(100, Math.round(fraction * 100))
  })()

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-zinc-800 bg-zinc-900/40 p-5">
        <h2 className="mb-4 text-sm font-semibold uppercase tracking-wider text-zinc-400">Run a backtest</h2>
        <div className="grid grid-cols-1 gap-4 md:grid-cols-4">
          <Field label="Strategy preset">
            <select
              value={presetId ?? ''}
              onChange={e => setPresetId(e.target.value ? Number(e.target.value) : null)}
              className="w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm"
              disabled={active}
            >
              <option value="">Pick one…</option>
              {presets.data?.map(p => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Stock count">
            <NumberInput value={stockCount} onChange={setStockCount} min={1} max={500} disabled={active} suffix="stocks" />
            <QuickPicks options={[5, 50, 200, 500]} active={stockCount} onPick={setStockCount} disabled={active} />
          </Field>
          <Field label="Backtest days">
            <NumberInput value={backtestDays} onChange={setBacktestDays} min={1} max={1000} disabled={active} suffix="days" />
            <QuickPicks options={[5, 30, 90, 365]} active={backtestDays} onPick={setBacktestDays} disabled={active} />
          </Field>
          <Field label="Timeframe">
            <select
              value={timeframe}
              onChange={e => setTimeframe(e.target.value)}
              className="w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm"
              disabled={active}
            >
              {['1min','5min','15min','25min','60min','1day'].map(tf =>
                <option key={tf} value={tf}>{tf}</option>)}
            </select>
          </Field>
        </div>
        <label className="mt-4 flex items-start gap-2 text-sm text-zinc-300">
          <input
            type="checkbox"
            checked={enableLog}
            onChange={e => setEnableLog(e.target.checked)}
            disabled={active}
            className="mt-0.5 h-4 w-4 rounded border-zinc-600 bg-zinc-900 accent-emerald-500 disabled:opacity-50"
          />
          <span>
            Enable detailed log
            <span className="ml-2 text-xs text-zinc-500">
              Records why every screened stock was kept or dropped (downloadable JSONL on the
              Results tab). Large file — leave off unless you're validating the strategy.
            </span>
          </span>
        </label>
        <div className="mt-4 flex gap-2">
          <button
            onClick={() => start.mutate()}
            disabled={presetId === null || active || start.isPending}
            className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {start.isPending ? 'Queuing…' : 'Start run'}
          </button>
          {active && (
            <button
              onClick={() => cancel.mutate()}
              disabled={cancel.isPending}
              className="rounded-md border border-red-700 bg-red-900/30 px-4 py-2 text-sm text-red-200 hover:bg-red-900/60 disabled:opacity-50"
            >
              {cancel.isPending ? 'Stopping…' : 'Stop'}
            </button>
          )}
        </div>
        {start.error && <p className="mt-3 text-sm text-red-400">{String(start.error)}</p>}
      </section>

      {progress && (
        <section className="rounded-lg border border-zinc-800 bg-zinc-900/40 p-5">
          <div className="mb-3 flex items-center justify-between">
            <h2 className="text-sm font-semibold uppercase tracking-wider text-zinc-400">
              Run #{progress.runId} · <span className="text-zinc-300">{progress.status}</span>
            </h2>
            <span className="text-xs text-zinc-500">
              {progress.daysProcessed}/{progress.totalDaysPlanned} days · chunk {progress.currentChunk}/{progress.totalChunks} · {pct}%
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-zinc-800">
            <div
              className={'h-full transition-all ' + (active ? 'bg-emerald-500' : progress.status === 'failed' ? 'bg-red-500' : progress.status === 'cancelled' ? 'bg-amber-500' : 'bg-emerald-500')}
              style={{ width: `${pct}%` }}
            />
          </div>
          {progress.fetch && active && (
            <p className="mt-3 text-xs text-zinc-400">
              <span className="inline-block h-2 w-2 animate-pulse rounded-full bg-emerald-400 align-middle"></span>
              <span className="ml-2 align-middle">
                Fetching candles · {progress.fetch.symbol} ({progress.fetch.stocksProcessed}/{progress.fetch.totalStocks} stocks)
              </span>
            </p>
          )}
          {progress.error && <p className="mt-3 text-sm text-red-400">{progress.error}</p>}
          {finished && progress.summary && (
            <div className="mt-4 grid grid-cols-3 gap-4 text-sm">
              <Stat label="Trades" value={String(progress.summary.tradeCount)} />
              <Stat label="Total P&L" value={`₹${progress.summary.totalPnL.toFixed(2)}`} />
              <Stat label="Win rate" value={`${(progress.summary.winRate * 100).toFixed(1)}%`} />
            </div>
          )}

          <div className="mt-4 max-h-72 overflow-y-auto rounded border border-zinc-800">
            <table className="w-full text-xs">
              <thead className="sticky top-0 bg-zinc-900 text-left text-zinc-500">
                <tr><th className="p-2">Date</th><th className="p-2">Symbol</th><th className="p-2">Exit</th><th className="p-2 text-right">P&L</th></tr>
              </thead>
              <tbody>
                {progress.trades.slice(-200).map(t => (
                  <tr key={t.id} className="border-t border-zinc-800">
                    <td className="p-2">{t.date.slice(0, 10)}</td>
                    <td className="p-2">{t.symbol}</td>
                    <td className="p-2 text-zinc-400">{t.exitReason}</td>
                    <td className={'p-2 text-right ' + (t.pnL >= 0 ? 'text-emerald-400' : 'text-red-400')}>
                      ₹{t.pnL.toFixed(2)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="block text-xs font-medium text-zinc-400">{label}</label>
      <div className="mt-1">{children}</div>
    </div>
  )
}

function NumberInput({
  value, onChange, min, max, disabled, suffix,
}: {
  value: number
  onChange: (n: number) => void
  min?: number
  max?: number
  disabled?: boolean
  suffix?: string
}) {
  // Local text state so the user can transiently clear the field while typing
  // without us snapping back to the parent value mid-keystroke.
  const [text, setText] = useState(String(value))
  useEffect(() => { setText(String(value)) }, [value])

  const commit = (raw: string) => {
    if (raw === '') {
      const fallback = min ?? 0
      setText(String(fallback))
      onChange(fallback)
      return
    }
    const n = Number(raw)
    const clamped = Math.min(max ?? n, Math.max(min ?? 0, n))
    setText(String(clamped))
    onChange(clamped)
  }

  return (
    <div className="relative">
      <input
        type="text"
        inputMode="numeric"
        pattern="[0-9]*"
        value={text}
        disabled={disabled}
        onChange={e => {
          const raw = e.target.value.replace(/\D/g, '')
          setText(raw)
          if (raw !== '') onChange(Number(raw))
        }}
        onBlur={e => commit(e.target.value.replace(/\D/g, ''))}
        className={
          'w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm tabular-nums focus:border-emerald-500 focus:outline-none disabled:opacity-50 ' +
          (suffix ? 'pr-14' : '')
        }
      />
      {suffix && (
        <span className="pointer-events-none absolute inset-y-0 right-3 flex items-center text-xs text-zinc-500">
          {suffix}
        </span>
      )}
    </div>
  )
}

function QuickPicks({
  options, active, onPick, disabled,
}: {
  options: number[]
  active: number
  onPick: (n: number) => void
  disabled?: boolean
}) {
  return (
    <div className="mt-1.5 flex gap-1">
      {options.map(opt => (
        <button
          key={opt}
          type="button"
          disabled={disabled}
          onClick={() => onPick(opt)}
          className={
            'rounded px-2 py-0.5 text-[11px] font-medium transition disabled:cursor-not-allowed disabled:opacity-40 ' +
            (active === opt
              ? 'border border-emerald-700 bg-emerald-500/15 text-emerald-300'
              : 'border border-zinc-800 text-zinc-500 hover:border-zinc-600 hover:text-zinc-200')
          }
        >
          {opt}
        </button>
      ))}
    </div>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-zinc-800 bg-zinc-900 p-3">
      <div className="text-xs text-zinc-500">{label}</div>
      <div className="mt-1 text-lg font-semibold">{value}</div>
    </div>
  )
}
