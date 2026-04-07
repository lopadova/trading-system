/**
 * Database Row Types
 * Typed interfaces for D1 query results
 */

// Supervisor Schema Types

export interface ServiceHeartbeatRow {
  service_name: string
  hostname: string
  last_seen_at: string
  uptime_seconds: number
  cpu_percent: number
  ram_percent: number
  disk_free_gb: number
  trading_mode: string
  version: string
  created_at: string
  updated_at: string
}

export interface SyncOutboxRow {
  event_id: string
  event_type: string
  payload_json: string
  dedupe_key: string | null
  status: 'pending' | 'sent' | 'failed'
  retry_count: number
  last_error: string | null
  next_retry_at: string | null
  created_at: string
  sent_at: string | null
}

export interface AlertHistoryRow {
  alert_id: string
  alert_type: string
  severity: 'info' | 'warning' | 'critical'
  message: string
  details_json: string | null
  source_service: string
  created_at: string
  resolved_at: string | null
  resolved_by: string | null
}

export interface LogReaderStateRow {
  file_path: string
  last_position: number
  last_size: number
  updated_at: string
}

// Options Execution Schema Types

export interface ActivePositionRow {
  position_id: string
  campaign_id: string
  symbol: string
  contract_symbol: string
  strategy_name: string
  quantity: number
  entry_price: number
  current_price: number | null
  unrealized_pnl: number | null
  stop_loss: number | null
  take_profit: number | null
  opened_at: string
  updated_at: string
  metadata_json: string | null
}

export interface PositionHistoryRow {
  history_id: string
  position_id: string
  campaign_id: string
  symbol: string
  contract_symbol: string
  strategy_name: string
  quantity: number
  entry_price: number
  exit_price: number | null
  realized_pnl: number | null
  status: 'open' | 'closed' | 'rolled'
  opened_at: string
  closed_at: string | null
  created_at: string
  metadata_json: string | null
}

export interface ExecutionLogRow {
  execution_id: string
  order_id: string
  position_id: string | null
  campaign_id: string
  symbol: string
  contract_symbol: string
  side: 'BUY' | 'SELL'
  quantity: number
  fill_price: number
  commission: number
  executed_at: string
  created_at: string
}

export interface StrategyStateRow {
  campaign_id: string
  strategy_name: string
  state_json: string
  updated_at: string
}
