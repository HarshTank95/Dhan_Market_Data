import { useEffect, useRef, useState } from 'react'
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'

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

// Subscribes to /hubs/backtest, joins group "run-{runId}" and exposes the
// merged event stream as a single React state object.
export function useBacktestProgress(runId: number | null): RunProgress | null {
  const [state, setState] = useState<RunProgress | null>(null)
  const connRef = useRef<HubConnection | null>(null)

  useEffect(() => {
    if (runId === null) {
      setState(null)
      return
    }
    setState(initial(runId))

    const conn = new HubConnectionBuilder()
      .withUrl('/hubs/backtest')
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build()
    connRef.current = conn

    conn.on('RunStarted', (msg: { runId: number; totalDaysPlanned: number }) =>
      setState(s => s && { ...s, status: 'running', totalDaysPlanned: msg.totalDaysPlanned }))

    conn.on('ChunkProgress', (msg: { currentChunk: number; totalChunks: number; daysProcessed: number }) =>
      setState(s => s && { ...s, currentChunk: msg.currentChunk, totalChunks: msg.totalChunks, daysProcessed: msg.daysProcessed }))

    conn.on('TradeRecorded', (msg: { trade: TradeRecord }) =>
      setState(s => s && { ...s, trades: [...s.trades, msg.trade] }))

    conn.on('RunCompleted', (msg: { summary: RunProgress['summary'] }) =>
      setState(s => s && { ...s, status: 'completed', summary: msg.summary }))

    conn.on('RunFailed', (msg: { errorMessage: string }) =>
      setState(s => s && { ...s, status: 'failed', error: msg.errorMessage }))

    conn.on('RunCancelled', () =>
      setState(s => s && { ...s, status: 'cancelled' }))

    conn.start()
      .then(() => conn.invoke('JoinRun', runId))
      .catch(err => setState(s => s && { ...s, status: 'failed', error: String(err) }))

    return () => { conn.stop() }
  }, [runId])

  return state
}
