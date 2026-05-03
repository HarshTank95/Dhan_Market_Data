import { useState } from 'react'
import { useMutation, useQuery } from '@tanstack/react-query'

import { api } from '../lib/api'
import { useBacktestProgress } from '../lib/signalr'

export function RunPage() {
  const presets = useQuery({ queryKey: ['strategies'], queryFn: api.listStrategies })
  const [presetId, setPresetId] = useState<number | null>(null)
  const [stockCount, setStockCount] = useState(500)
  const [backtestDays, setBacktestDays] = useState(50)
  const [timeframe, setTimeframe] = useState('5min')
  const [activeRunId, setActiveRunId] = useState<number | null>(null)

  const progress = useBacktestProgress(activeRunId)

  const start = useMutation({
    mutationFn: () => api.startRun({
      presetId: presetId!,
      stockCount,
      backtestDays,
      timeframe,
      exchangeSegment: 'NSE_EQ',
    }),
    onSuccess: r => setActiveRunId(r.runId),
  })

  const cancel = useMutation({
    mutationFn: () => api.cancelRun(activeRunId!),
  })

  const running = progress?.status === 'running'
  const finished = progress?.status === 'completed' || progress?.status === 'failed' || progress?.status === 'cancelled'

  const pct = progress && progress.totalDaysPlanned > 0
    ? Math.round((progress.daysProcessed / progress.totalDaysPlanned) * 100)
    : 0

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
              disabled={running}
            >
              <option value="">Pick one…</option>
              {presets.data?.map(p => (
                <option key={p.id} value={p.id}>{p.name}</option>
              ))}
            </select>
          </Field>
          <Field label="Stock count">
            <NumberInput value={stockCount} onChange={setStockCount} min={1} max={500} disabled={running} />
          </Field>
          <Field label="Backtest days">
            <NumberInput value={backtestDays} onChange={setBacktestDays} min={1} max={1000} disabled={running} />
          </Field>
          <Field label="Timeframe">
            <select
              value={timeframe}
              onChange={e => setTimeframe(e.target.value)}
              className="w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm"
              disabled={running}
            >
              {['1min','5min','15min','25min','60min','1day'].map(tf =>
                <option key={tf} value={tf}>{tf}</option>)}
            </select>
          </Field>
        </div>
        <div className="mt-4 flex gap-2">
          <button
            onClick={() => start.mutate()}
            disabled={presetId === null || running || start.isPending}
            className="rounded-md bg-emerald-600 px-4 py-2 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
          >
            {start.isPending ? 'Queuing…' : 'Start run'}
          </button>
          {running && (
            <button
              onClick={() => cancel.mutate()}
              disabled={cancel.isPending}
              className="rounded-md border border-zinc-700 px-4 py-2 text-sm hover:bg-zinc-800"
            >
              {cancel.isPending ? 'Cancelling…' : 'Cancel'}
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
              {progress.daysProcessed}/{progress.totalDaysPlanned} days · chunk {progress.currentChunk}/{progress.totalChunks}
            </span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-zinc-800">
            <div
              className="h-full bg-emerald-500 transition-all"
              style={{ width: `${pct}%` }}
            />
          </div>
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
  value, onChange, min, max, disabled,
}: { value: number; onChange: (n: number) => void; min?: number; max?: number; disabled?: boolean }) {
  return (
    <input
      type="number"
      value={value}
      min={min}
      max={max}
      disabled={disabled}
      onChange={e => onChange(Number(e.target.value))}
      className="w-full rounded-md border border-zinc-700 bg-zinc-900 px-3 py-2 text-sm"
    />
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
