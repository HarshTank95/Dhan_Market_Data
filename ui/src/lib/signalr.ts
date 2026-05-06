import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

import { api } from './api'
import type { TradeRecord } from '../types'

export interface RunProgress {
  runId: number
  totalDaysPlanned: number
  daysProcessed: number
  currentChunk: number
  totalChunks: number
  status: 'idle' | 'running' | 'completed' | 'failed' | 'cancelled'
  error?: string
  trades: TradeRecord[]
  fetch?: {
    stocksProcessed: number
    totalStocks: number
    symbol: string
  }
  summary?: {
    tradeCount: number
    totalPnL: number
    winRate: number
    exitBreakdown: Record<string, number>
  }
}

const initial = (runId: number): RunProgress => ({
  runId,
  totalDaysPlanned: 0,
  daysProcessed: 0,
  currentChunk: 0,
  totalChunks: 0,
  status: 'idle',
  trades: [],
})

// Server returns RunStatus as a number via default JsonSerializer; keep a
// string fallback in case the contract ever flips to enum-as-string.
function mapStatus(serverStatus: number | string): RunProgress['status'] | undefined {
  const s = typeof serverStatus === 'string' ? serverStatus.toLowerCase() : serverStatus
  // 0 Queued · 1 Running · 2 Completed · 3 Failed · 4 Cancelling · 5 Cancelled
  if (s === 0 || s === 'queued') return 'idle'
  if (s === 1 || s === 'running' || s === 4 || s === 'cancelling') return 'running'
  if (s === 2 || s === 'completed') return 'completed'
  if (s === 3 || s === 'failed') return 'failed'
  if (s === 5 || s === 'cancelled') return 'cancelled'
  return undefined
}

function isTerminal(status: RunProgress['status']): boolean {
  return status === 'completed' || status === 'failed' || status === 'cancelled'
}

function summarise(trades: TradeRecord[], totalPnL: number): RunProgress['summary'] {
  const winRate = trades.length > 0 ? trades.filter(t => t.pnL > 0).length / trades.length : 0
  const exitBreakdown: Record<string, number> = {}
  for (const t of trades) exitBreakdown[t.exitReason] = (exitBreakdown[t.exitReason] ?? 0) + 1
  return { tradeCount: trades.length, totalPnL, winRate, exitBreakdown }
}

// Subscribes to /hubs/backtest, joins group "run-{runId}", hydrates current
// state from the REST API (so reattaching mid-run shows progress + trades),
// and exposes the merged event stream as a single React state object.
export function useBacktestProgress(runId: number | null): RunProgress | null {
  const [state, setState] = useState<RunProgress | null>(null)
  const connRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    if (runId === null) {
      setState(null)
      return
    }
    setState(initial(runId))
    let cancelled = false

    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/backtest')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connRef.current = conn

    conn.on('RunStarted', (msg: { runId: number; totalDaysPlanned: number }) =>
      setState(s => s && { ...s, status: 'running', totalDaysPlanned: msg.totalDaysPlanned }))

    conn.on('FetchProgress', (msg: { stocksProcessed: number; totalStocks: number; symbol: string; currentChunk: number; totalChunks: number }) =>
      setState(s => s && {
        ...s,
        status: s.status === 'idle' ? 'running' : s.status,
        currentChunk: msg.currentChunk,
        totalChunks: msg.totalChunks,
        fetch: { stocksProcessed: msg.stocksProcessed, totalStocks: msg.totalStocks, symbol: msg.symbol },
      }))

    conn.on('ChunkProgress', (msg: { currentChunk: number; totalChunks: number; daysProcessed: number }) =>
      setState(s => s && { ...s, currentChunk: msg.currentChunk, totalChunks: msg.totalChunks, daysProcessed: msg.daysProcessed, fetch: undefined }))

    // De-dup by id: the same trade can arrive once via hydration and once via SignalR
    // if the broadcast happens between the HTTP fetch and the JoinRun handshake.
    conn.on('TradeRecorded', (msg: { trade: TradeRecord }) =>
      setState(s => {
        if (!s) return s
        if (s.trades.some(t => t.id === msg.trade.id)) return s
        return { ...s, trades: [...s.trades, msg.trade] }
      }))

    conn.on('RunCompleted', (msg: { summary: RunProgress['summary'] }) =>
      setState(s => s && { ...s, status: 'completed', summary: msg.summary }))

    conn.on('RunFailed', (msg: { errorMessage: string }) =>
      setState(s => s && { ...s, status: 'failed', error: msg.errorMessage }))

    conn.on('RunCancelled', () =>
      setState(s => s && { ...s, status: 'cancelled' }))

    const hydrate = async () => {
      try {
        const [run, trades] = await Promise.all([
          api.getRun(runId),
          api.getRunTrades(runId, 1, 1000),
        ])
        if (cancelled) return
        const mapped = mapStatus(run.status as unknown as number | string)
        setState(s => {
          if (!s) return s
          // Merge: only fill fields if SignalR hasn't already advanced past them.
          const next: RunProgress = {
            ...s,
            status: mapped ?? s.status,
            totalDaysPlanned: Math.max(s.totalDaysPlanned, run.totalDaysPlanned),
            daysProcessed: Math.max(s.daysProcessed, run.totalDaysProcessed),
            error: run.errorMessage ?? s.error,
          }
          // Prepend any hydrated trades the live stream hasn't seen.
          const seen = new Set(s.trades.map(t => t.id))
          const hydrated = trades.trades.filter(t => !seen.has(t.id))
          if (hydrated.length > 0) {
            next.trades = [...hydrated, ...s.trades]
          }
          // Synthesise a summary for terminal runs so the totals card renders.
          if (mapped && isTerminal(mapped) && !s.summary) {
            next.summary = summarise(next.trades, run.totalPnL)
          }
          return next
        })
      } catch {
        // Hydration is best-effort — live SignalR events still drive the UI.
      }
    }

    conn.start()
      .then(() => conn.invoke('JoinRun', runId))
      .then(() => hydrate())
      .catch(err => setState(s => s && { ...s, status: 'failed', error: String(err) }))

    return () => {
      cancelled = true
      conn.stop()
    }
  }, [runId])

  return state
}
