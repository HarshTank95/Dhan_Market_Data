// Thin typed fetch wrapper. All requests go through Vite's proxy to the API.

async function request<T>(
  path: string,
  init?: RequestInit & { json?: unknown },
): Promise<T> {
  const headers: Record<string, string> = {
    Accept: 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
  }
  let body: BodyInit | undefined
  if (init?.json !== undefined) {
    headers['Content-Type'] = 'application/json'
    body = JSON.stringify(init.json)
  } else if (init?.body) {
    body = init.body
  }

  const res = await fetch(path, { ...init, headers, body })
  if (!res.ok) {
    const text = await res.text().catch(() => '')
    throw new Error(`${res.status} ${res.statusText}${text ? ` — ${text}` : ''}`)
  }
  if (res.status === 204) return undefined as T
  const ct = res.headers.get('content-type') ?? ''
  return ct.includes('application/json') ? (await res.json()) as T : (await res.text()) as T
}

import type {
  BacktestRunDetail,
  BacktestRunSummary,
  CredentialsStatus,
  RegistryEntry,
  StartRunRequest,
  StrategyPresetDetail,
  StrategyPresetSummary,
  TradeList,
} from '../types'

export const api = {
  // Strategies
  listStrategies: () => request<StrategyPresetSummary[]>('/api/strategies'),
  getStrategy: (id: number) => request<StrategyPresetDetail>(`/api/strategies/${id}`),
  updateStrategy: (id: number, body: Partial<StrategyPresetDetail>) =>
    request<StrategyPresetDetail>(`/api/strategies/${id}`, { method: 'PUT', json: body }),
  resetStrategy: (id: number) =>
    request<StrategyPresetDetail>(`/api/strategies/${id}/reset`, { method: 'POST' }),
  cloneStrategy: (id: number, name: string) =>
    request<StrategyPresetDetail>(`/api/strategies/${id}/clone`, {
      method: 'POST',
      json: { name },
    }),
  deleteStrategy: (id: number) =>
    request<void>(`/api/strategies/${id}`, { method: 'DELETE' }),

  // Registry
  listScreeners: () => request<RegistryEntry[]>('/api/registry/screeners'),
  listStrategiesRegistry: () => request<RegistryEntry[]>('/api/registry/strategies'),

  // Runs
  startRun: (req: StartRunRequest) =>
    request<{ runId: number; status: string }>('/api/runs', {
      method: 'POST',
      json: req,
    }),
  cancelRun: (id: number) =>
    request<void>(`/api/runs/${id}`, { method: 'DELETE' }),
  listRuns: () => request<BacktestRunSummary[]>('/api/runs'),
  getRun: (id: number) => request<BacktestRunDetail>(`/api/runs/${id}`),
  getRunTrades: (id: number, page = 1, pageSize = 100) =>
    request<TradeList>(`/api/runs/${id}/trades?page=${page}&pageSize=${pageSize}`),
  csvUrl: (id: number) => `/api/runs/${id}/csv`,

  // Credentials
  getCredentials: () => request<CredentialsStatus>('/api/credentials'),
  setCredentials: (clientId: string, accessToken: string) =>
    request<CredentialsStatus>('/api/credentials', {
      method: 'PUT',
      json: { clientId, accessToken },
    }),
}
