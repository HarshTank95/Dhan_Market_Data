// Hand-written API contract types — mirror the C# DTOs in DhanMarketData.Api.Contracts.
// (When the project grows, replace with openapi-typescript-generated types.)

export interface StrategyPresetSummary {
  id: number
  name: string
  description: string
  isBuiltIn: boolean
  screenerType: string
  strategyType: string
  createdAt: string
  updatedAt: string
}

export interface StrategyPresetDetail extends StrategyPresetSummary {
  screenerConfig: Record<string, unknown> | null
  strategyConfig: Record<string, unknown> | null
  tradingConfig: Record<string, unknown> | null
}

export interface RegistryField {
  name: string
  label: string
  group: string
  kind: 'auto' | 'number' | 'integer' | 'percent' | 'currency' | 'multiplier' | 'timeofday' | 'boolean' | 'text'
  description?: string
  min?: number
  max?: number
  step?: number
  unit?: string
  order: number
  default: unknown
}

export interface RegistryEntry {
  key: string
  displayName: string
  description: string
  configClassName: string
  fields: RegistryField[]
}

export type RunStatus =
  | 'Queued' | 'Running' | 'Completed' | 'Failed' | 'Cancelling' | 'Cancelled'

export interface BacktestRunSummary {
  id: number
  strategyPresetId: number
  presetName: string
  status: RunStatus
  createdAt: string
  startedAt: string | null
  finishedAt: string | null
  totalDaysProcessed: number
  totalDaysPlanned: number
  tradeCount: number
  totalPnL: number
  errorMessage: string | null
}

export interface BacktestRunDetail extends BacktestRunSummary {
  timeframe: string
  exchangeSegment: string
  stockCount: number
  backtestDays: number
  screenerType: string
  strategyType: string
}

export interface TradeRecord {
  id: number
  symbol: string
  securityId: string
  date: string
  entryTime: string
  entryPrice: number
  quantity: number
  stopLoss: number
  target: number
  exitTime: string
  exitPrice: number
  exitReason: string
  pnL: number
  pnLPercent: number
}

export interface TradeList {
  trades: TradeRecord[]
  totalCount: number
  page: number
  pageSize: number
}

export interface CredentialsStatus {
  clientId: string
  hasToken: boolean
  tokenExpiresAt: string | null
  canGenerate: boolean
  updatedAt: string | null
}

export interface GenerateTokenResult {
  method: string
  tokenExpiresAt: string | null
}

export interface StartRunRequest {
  presetId: number
  stockCount: number
  backtestDays: number
  timeframe: string
  exchangeSegment: string
}
