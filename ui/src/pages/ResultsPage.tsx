import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'

import { api } from '../lib/api'
import { formatIstTime } from '../lib/datetime'

export function ResultsPage() {
  const qc = useQueryClient()
  const runs = useQuery({ queryKey: ['runs'], queryFn: api.listRuns, refetchInterval: 5000 })
  const [selectedId, setSelectedId] = useState<number | null>(null)
  const trades = useQuery({
    enabled: selectedId !== null,
    queryKey: ['runs', selectedId, 'trades'],
    queryFn: () => api.getRunTrades(selectedId!, 1, 500),
  })

  const selectedRun = runs.data?.find(r => r.id === selectedId)
  const anyLogs = runs.data?.some(r => r.hasDiagnosticLog) ?? false

  const deleteLog = useMutation({
    mutationFn: (id: number) => api.deleteLog(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  })
  const deleteAllLogs = useMutation({
    mutationFn: () => api.deleteAllLogs(),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['runs'] }),
  })

  return (
    <div className="grid grid-cols-12 gap-6">
      <aside className="col-span-5">
        <div className="mb-3 flex items-center justify-between">
          <h2 className="text-sm font-semibold uppercase tracking-wider text-zinc-400">Past runs</h2>
          {anyLogs && (
            <button
              onClick={() => { if (confirm('Delete ALL diagnostic log files?')) deleteAllLogs.mutate() }}
              disabled={deleteAllLogs.isPending}
              className="rounded border border-zinc-700 px-2 py-1 text-xs text-zinc-400 hover:bg-zinc-800 hover:text-red-300 disabled:opacity-50"
            >
              {deleteAllLogs.isPending ? 'Deleting…' : 'Delete all logs'}
            </button>
          )}
        </div>
        <div className="overflow-hidden rounded border border-zinc-800">
          <table className="w-full text-xs">
            <thead className="bg-zinc-900 text-left text-zinc-500">
              <tr>
                <th className="p-2">#</th><th className="p-2">Preset</th><th className="p-2">Status</th>
                <th className="p-2 text-right">Trades</th><th className="p-2 text-right">P&L</th>
              </tr>
            </thead>
            <tbody>
              {runs.data?.map(r => (
                <tr
                  key={r.id}
                  onClick={() => setSelectedId(r.id)}
                  className={'cursor-pointer border-t border-zinc-800 hover:bg-zinc-900 ' +
                    (selectedId === r.id ? 'bg-zinc-900' : '')}
                >
                  <td className="p-2">{r.id}</td>
                  <td className="p-2">{r.presetName}</td>
                  <td className="p-2 text-zinc-400">{r.status}</td>
                  <td className="p-2 text-right">{r.tradeCount}</td>
                  <td className={'p-2 text-right ' + (r.totalPnL >= 0 ? 'text-emerald-400' : 'text-red-400')}>
                    ₹{r.totalPnL.toFixed(2)}
                  </td>
                </tr>
              ))}
              {runs.data && runs.data.length === 0 && (
                <tr><td colSpan={5} className="p-4 text-center text-zinc-500">No runs yet.</td></tr>
              )}
            </tbody>
          </table>
        </div>
      </aside>

      <section className="col-span-7">
        {selectedId === null && <p className="text-zinc-500">Pick a run on the left.</p>}
        {selectedId !== null && (
          <>
            <div className="mb-3 flex items-center justify-between">
              <h2 className="text-sm font-semibold uppercase tracking-wider text-zinc-400">
                Trades · run #{selectedId}
              </h2>
              <div className="flex items-center gap-2">
                {selectedRun?.hasDiagnosticLog && (
                  <>
                    <a
                      href={api.logUrl(selectedId)}
                      className="rounded-md border border-zinc-700 px-3 py-1.5 text-sm hover:bg-zinc-800"
                      title="Per-stock screening decision funnel (JSONL)"
                    >
                      Download log
                    </a>
                    <button
                      onClick={() => { if (confirm(`Delete the diagnostic log for run #${selectedId}?`)) deleteLog.mutate(selectedId) }}
                      disabled={deleteLog.isPending}
                      className="rounded-md border border-zinc-700 px-3 py-1.5 text-sm text-zinc-400 hover:bg-zinc-800 hover:text-red-300 disabled:opacity-50"
                    >
                      Delete log
                    </button>
                  </>
                )}
                {selectedRun?.diagnosticLogEnabled && !selectedRun?.hasDiagnosticLog && (
                  <span className="text-xs text-zinc-600">log deleted</span>
                )}
                <a
                  href={api.csvUrl(selectedId)}
                  className="rounded-md border border-zinc-700 px-3 py-1.5 text-sm hover:bg-zinc-800"
                >
                  Download CSV
                </a>
              </div>
            </div>
            <div className="max-h-[600px] overflow-y-auto rounded border border-zinc-800">
              <table className="w-full text-xs">
                <thead className="sticky top-0 bg-zinc-900 text-left text-zinc-500">
                  <tr>
                    <th className="p-2">Date</th><th className="p-2">Symbol</th>
                    <th className="p-2">Time</th>
                    <th className="p-2">Entry</th><th className="p-2">Exit</th>
                    <th className="p-2">Reason</th><th className="p-2 text-right">P&L</th>
                  </tr>
                </thead>
                <tbody>
                  {trades.data?.trades.map(t => (
                    <tr key={t.id} className="border-t border-zinc-800">
                      <td className="p-2">{t.date.slice(0, 10)}</td>
                      <td className="p-2">{t.symbol}</td>
                      <td className="p-2 tabular-nums text-zinc-400">{formatIstTime(t.entryTime)}</td>
                      <td className="p-2">₹{t.entryPrice.toFixed(2)}</td>
                      <td className="p-2">₹{t.exitPrice.toFixed(2)}</td>
                      <td className="p-2 text-zinc-400">{t.exitReason}</td>
                      <td className={'p-2 text-right ' + (t.pnL >= 0 ? 'text-emerald-400' : 'text-red-400')}>
                        ₹{t.pnL.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>
    </div>
  )
}
