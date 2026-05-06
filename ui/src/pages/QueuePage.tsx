import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api } from '../lib/api'

// Server returns RunStatus as a number; keep string fallback in case it ever flips.
type ServerStatus = number | string

// RunStatus enum (server): 0 Queued · 1 Running · 2 Completed · 3 Failed · 4 Cancelling · 5 Cancelled
const ACTIVE_STATUS_VALUES: Array<ServerStatus> = [0, 1, 4, 'Queued', 'Running', 'Cancelling']
const ACTIVE_STATUSES = new Set<ServerStatus>(ACTIVE_STATUS_VALUES)

type StatusKey = 'queued' | 'running' | 'cancelling' | 'other'

function statusKey(s: ServerStatus): StatusKey {
  if (s === 0 || s === 'Queued') return 'queued'
  if (s === 1 || s === 'Running') return 'running'
  if (s === 4 || s === 'Cancelling') return 'cancelling'
  return 'other'
}

function statusLabel(s: ServerStatus): string {
  switch (statusKey(s)) {
    case 'queued': return 'Queued'
    case 'running': return 'Running'
    case 'cancelling': return 'Cancelling'
    default: return String(s)
  }
}

function relativeTime(iso: string | null): string {
  if (!iso) return '—'
  const ms = Date.now() - new Date(iso).getTime()
  if (ms < 0 || ms < 1000) return 'just now'
  const sec = Math.floor(ms / 1000)
  if (sec < 60) return `${sec}s ago`
  const min = Math.floor(sec / 60)
  if (min < 60) return `${min}m ago`
  const hr = Math.floor(min / 60)
  if (hr < 24) return `${hr}h ago`
  return `${Math.floor(hr / 24)}d ago`
}

export function QueuePage() {
  const qc = useQueryClient()
  const runs = useQuery({
    queryKey: ['runs', 'queue'],
    queryFn: api.listRuns,
    refetchInterval: 1500,
  })

  const activeRuns = (runs.data ?? []).filter(r =>
    ACTIVE_STATUSES.has(r.status as unknown as ServerStatus),
  )

  const stopOne = useMutation({
    mutationFn: (id: number) => api.cancelRun(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  })

  const stopAll = useMutation({
    mutationFn: () => api.cancelAllActive(),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  })

  return (
    <div className="space-y-6">
      <section className="rounded-lg border border-zinc-800 bg-zinc-900/40 p-5">
        <div className="mb-5 flex items-start justify-between gap-4">
          <div>
            <h2 className="text-sm font-semibold uppercase tracking-wider text-zinc-400">
              Backtest queue
            </h2>
            <p className="mt-1 text-xs text-zinc-500">
              {activeRuns.length === 0
                ? 'Nothing running. The runner is idle.'
                : `${activeRuns.length} active backtest${activeRuns.length === 1 ? '' : 's'} · live (auto-refreshes every 1.5s)`}
            </p>
          </div>
          {activeRuns.length > 1 && (
            <button
              onClick={() => stopAll.mutate()}
              disabled={stopAll.isPending}
              className="rounded-md border border-red-700 bg-red-900/30 px-4 py-2 text-sm font-medium text-red-200 hover:bg-red-900/60 disabled:opacity-50"
            >
              {stopAll.isPending ? 'Stopping all…' : `Stop all (${activeRuns.length})`}
            </button>
          )}
        </div>

        {/* No "Loading…" state — once data is cached, refetches happen silently in
            the background. The empty state below covers both initial-load and "truly
            no active runs" without flickering. */}
        {activeRuns.length === 0 && (
          <div className="rounded-md border border-dashed border-zinc-800 bg-zinc-950 p-10 text-center">
            <p className="text-sm text-zinc-300">No backtests running or queued.</p>
            <p className="mt-1 text-xs text-zinc-500">
              Start one from the <span className="text-emerald-400">Run</span> tab.
            </p>
          </div>
        )}

        {activeRuns.length > 0 && (
          <div className="overflow-hidden rounded border border-zinc-800">
            <table className="w-full text-sm">
              <thead className="bg-zinc-900 text-left text-xs uppercase tracking-wider text-zinc-500">
                <tr>
                  <th className="p-3">Run</th>
                  <th className="p-3">Preset</th>
                  <th className="p-3">Status</th>
                  <th className="p-3">Progress</th>
                  <th className="p-3">Created</th>
                  <th className="p-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody>
                {activeRuns.map(r => {
                  const key = statusKey(r.status as unknown as ServerStatus)
                  const pct = r.totalDaysPlanned > 0
                    ? Math.round((r.totalDaysProcessed / r.totalDaysPlanned) * 100)
                    : 0
                  const badge =
                    key === 'running'
                      ? 'border-emerald-700 bg-emerald-500/10 text-emerald-300'
                      : key === 'queued'
                        ? 'border-amber-800 bg-amber-500/10 text-amber-300'
                        : 'border-red-700 bg-red-500/10 text-red-300'
                  const stoppingThisOne = stopOne.isPending && stopOne.variables === r.id
                  return (
                    <tr key={r.id} className="border-t border-zinc-800 hover:bg-zinc-900/50">
                      <td className="p-3 font-mono text-zinc-400">#{r.id}</td>
                      <td className="p-3">{r.presetName}</td>
                      <td className="p-3">
                        <span className={'inline-flex items-center rounded border px-2 py-0.5 text-xs ' + badge}>
                          {key === 'running' && (
                            <span className="mr-1.5 inline-block h-1.5 w-1.5 animate-pulse rounded-full bg-emerald-400" />
                          )}
                          {statusLabel(r.status as unknown as ServerStatus)}
                        </span>
                      </td>
                      <td className="p-3 text-xs text-zinc-400">
                        {key === 'queued'
                          ? <span className="text-zinc-500">Waiting in queue…</span>
                          : key === 'cancelling'
                            ? <span className="text-red-400">Stopping…</span>
                            : (
                              <div className="flex items-center gap-2">
                                <div className="h-1.5 w-24 overflow-hidden rounded-full bg-zinc-800">
                                  <div className="h-full bg-emerald-500" style={{ width: `${pct}%` }} />
                                </div>
                                <span>{r.totalDaysProcessed}/{r.totalDaysPlanned} · {pct}%</span>
                              </div>
                            )}
                      </td>
                      <td className="p-3 text-xs text-zinc-500">{relativeTime(r.createdAt)}</td>
                      <td className="p-3 text-right">
                        <button
                          onClick={() => stopOne.mutate(r.id)}
                          disabled={stoppingThisOne || key === 'cancelling'}
                          className="rounded border border-red-700 bg-red-900/20 px-3 py-1 text-xs text-red-200 hover:bg-red-900/50 disabled:cursor-not-allowed disabled:opacity-50"
                        >
                          {stoppingThisOne ? 'Stopping…' : key === 'cancelling' ? 'Stopping…' : 'Stop'}
                        </button>
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

        {stopAll.isSuccess && stopAll.data && (
          <p className="mt-3 text-xs text-zinc-500">
            Cancelled {stopAll.data.cancelledCount} run{stopAll.data.cancelledCount === 1 ? '' : 's'}.
          </p>
        )}
        {stopAll.error && (
          <p className="mt-3 text-xs text-red-400">{String(stopAll.error)}</p>
        )}
      </section>
    </div>
  )
}
