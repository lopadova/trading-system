# T-SW-01a — SDF v1 TypeScript Types Complete

## Obiettivo
Creare tutti i tipi TypeScript per SDF v1, specchiando 1:1 i modelli C#.
Questa è la fondazione type-safe per tutti i task successivi del wizard.

## Dipendenze
Nessuna. Questo è il primo task della feature.

## Files da Creare
- `dashboard/src/types/sdf-v1.ts`

## Files da Modificare
Nessuno.

## Implementazione

### sdf-v1.ts — Tipi completi

```typescript
// Enums
export type OptionRight = 'put' | 'call'
export type SettlementPreference = 'PM' | 'AM'
export type FallbackBehavior = 'block' | 'warn' | 'allow'
export type ScoringMethod = 'min_delta_distance' | 'max_oi' | 'min_spread'
export type OrderType = 'limit_mid' | 'limit_ask' | 'limit_bid' | 'market'
export type HardStopType = 'underlying_vs_leg_strike' | 'portfolio_greek' | 'pnl_threshold'
export type GreekName = 'delta' | 'theta' | 'gamma' | 'vega'
export type ComparisonOperator = 'lt' | 'gt' | 'lte' | 'gte'
export type Severity = 'critical' | 'high' | 'medium'
export type CloseSequence = 'all_legs' | 'combo_only' | 'hedge_only'
export type OpeningSequence = 'combo_first' | 'hedge_first' | 'simultaneous'
export type RepricingFallback = 'cancel_and_block' | 'market' | 'cancel_and_alert'
export type WeekStartDay = 'monday' | 'sunday'

// Leg definition
export interface StrategyLeg {
  leg_id: string
  action: 'buy' | 'sell'
  right: OptionRight
  target_dte: number
  dte_tolerance: number
  target_delta: number
  delta_tolerance: number
  quantity: number
  settlement_preference: SettlementPreference
  exclude_expiry_within_days: number
  role: string
  order_group: 'combo' | 'standalone'
  open_sequence?: string
}

// Hard stop condition
export interface HardStopCondition {
  condition_id: string
  type: HardStopType
  reference_leg_id?: string
  greek?: GreekName
  operator: ComparisonOperator
  threshold: number
  severity: Severity
  close_sequence: CloseSequence
}

// Changelog entry
export interface ChangelogEntry {
  version: string
  date: string
  notes: string
}

// Instrument configuration
export interface InstrumentConfig {
  type: 'options'
  underlying_symbol: string
  underlying_sec_type: 'IND' | 'STK' | 'ETF'
  underlying_exchange: string
  options_exchange: string
  currency: string
  multiplier: number
  option_right: OptionRight
}

// IVTS filter configuration
export interface IvtsFilter {
  enabled: boolean
  formula: string
  suspend_threshold: number
  resume_threshold: number
  staleness_max_minutes: number
  fallback_behavior: FallbackBehavior
}

// Safe execution window
export interface SafeExecutionWindow {
  enabled: boolean
  exclude_first_minutes: number
  exclude_last_minutes: number
}

// Entry filters
export interface EntryFilters {
  ivts: IvtsFilter
  market_hours_only: boolean
  safe_execution_window: SafeExecutionWindow
}

// Campaign rules
export interface CampaignRules {
  max_active_campaigns: number
  max_per_rolling_week: number
  week_start_day: WeekStartDay
  overlap_check_enabled: boolean
}

// Structure
export interface StrategyStructure {
  legs: StrategyLeg[]
  protection_legs: StrategyLeg[]
}

// Selection filters
export interface SelectionFilters {
  min_open_interest: number
  max_spread_pct_of_mid: number
  scoring_method: ScoringMethod
}

// Exit rules
export interface ExitRules {
  profit_target_usd: number
  stop_loss_usd: number
  max_days_in_position: number
  hard_stop_conditions: HardStopCondition[]
}

// Repricing configuration
export interface RepricingConfig {
  enabled: boolean
  max_attempts: number
  interval_seconds: number
  step_pct_of_half_spread: number
  max_slippage_pct_from_first_mid: number
  fallback_on_max_attempts: RepricingFallback
}

// GTC target order configuration
export interface GtcTargetOrder {
  enabled: boolean
  submit_immediately_after_fill: boolean
}

// Execution rules
export interface ExecutionRules {
  order_type: OrderType
  repricing: RepricingConfig
  opening_sequence: OpeningSequence
  margin_buffer_pct: number
  what_if_check_enabled: boolean
  gtc_target_order: GtcTargetOrder
}

// Monitoring configuration
export interface MonitoringConfig {
  greeks_snapshot_interval_minutes: number
  risk_check_interval_minutes: number
}

// Notifications configuration
export interface NotificationsConfig {
  on_campaign_opened: boolean
  on_target_hit: boolean
  on_stop_loss_hit: boolean
  on_hard_stop_triggered: boolean
  on_max_days_close: boolean
  on_ivts_state_change: boolean
}

// Complete strategy definition (SDF v1)
export interface StrategyDefinition {
  $schema?: string
  strategy_id: string
  strategy_version: string
  schema_version: 1
  name: string
  author: string
  author_url?: string
  license: string
  description: string
  tags: string[]
  created_at: string
  updated_at: string
  enabled_default: boolean
  changelog: ChangelogEntry[]
  instrument: InstrumentConfig
  entry_filters: EntryFilters
  campaign_rules: CampaignRules
  structure: StrategyStructure
  selection_filters: SelectionFilters
  exit_rules: ExitRules
  execution_rules: ExecutionRules
  monitoring: MonitoringConfig
  notifications: NotificationsConfig
}

// Utility types
export type DeepPartial<T> = T extends object ? {
  [P in keyof T]?: DeepPartial<T[P]>;
} : T;

export type StrategyDraft = DeepPartial<StrategyDefinition>

// Type guards
export function isStrategyDefinition(obj: unknown): obj is StrategyDefinition {
  if (typeof obj !== 'object' || obj === null) return false
  const s = obj as Record<string, unknown>
  return (
    typeof s.strategy_id === 'string' &&
    typeof s.schema_version === 'number' &&
    s.schema_version === 1 &&
    typeof s.name === 'string'
  )
}

export function isStrategyLeg(obj: unknown): obj is StrategyLeg {
  if (typeof obj !== 'object' || obj === null) return false
  const l = obj as Record<string, unknown>
  return (
    typeof l.leg_id === 'string' &&
    (l.action === 'buy' || l.action === 'sell') &&
    typeof l.target_delta === 'number'
  )
}
```

## Test

- `TEST-SW-01a-01`: Import di sdf-v1.ts compila senza errori TypeScript
- `TEST-SW-01a-02`: `tsc --strict` su sdf-v1.ts → 0 errori
- `TEST-SW-01a-03`: `isStrategyDefinition({})` → false
- `TEST-SW-01a-04`: `isStrategyDefinition(validSDF)` → true
- `TEST-SW-01a-05`: `isStrategyLeg(validLeg)` → true
- `TEST-SW-01a-06`: DeepPartial<StrategyDefinition> permette draft parziali
- `TEST-SW-01a-07`: Ogni type enum ha valori corretti (match con C#)
- `TEST-SW-01a-08`: StrategyDefinition ha tutti i campi del C# model

## Done Criteria

- [ ] `tsc --strict` compila senza errori
- [ ] Tutti i test TEST-SW-01a-XX passano
- [ ] Type guards testati con oggetti validi/invalidi
- [ ] DeepPartial consente draft parziali senza errori
- [ ] File documentato con JSDoc su ogni interface principale

## Stima

~4 ore
