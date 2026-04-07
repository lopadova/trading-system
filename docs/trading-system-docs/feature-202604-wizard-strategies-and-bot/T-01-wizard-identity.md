# T-SW-01 — SDF v1 TypeScript Types + Client Validator + Defaults

## Obiettivo
Creare la fondazione TypeScript del wizard: tipi completi SDF v1, validatore
client-side con messaggi in italiano, valori default per ogni campo, utility
per generazione strategy_id automatica. È il blocco fondante di tutti i task SW successivi.

## Dipendenze
Nessuna. Questo task non dipende da altri.

## Files da Creare
- `dashboard/src/types/sdf-v1.ts`
- `dashboard/src/utils/sdf-validator.ts`
- `dashboard/src/utils/sdf-defaults.ts`
- `dashboard/src/utils/sdf-validator.test.ts`

## Files da Modificare
Nessuno.

## Implementazione

### sdf-v1.ts — Tipi completi (estratto chiave)

```typescript
// Tutti i tipi SDF v1 — devono specchiare 1:1 i modelli C# di T-15
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
  changelog: Array<{ version: string; date: string; notes: string }>
  instrument: {
    type: 'options'
    underlying_symbol: string
    underlying_sec_type: 'IND' | 'STK' | 'ETF'
    underlying_exchange: string
    options_exchange: string
    currency: string
    multiplier: number
    option_right: OptionRight
  }
  entry_filters: {
    ivts: {
      enabled: boolean
      formula: string
      suspend_threshold: number
      resume_threshold: number
      staleness_max_minutes: number
      fallback_behavior: FallbackBehavior
    }
    market_hours_only: boolean
    safe_execution_window: {
      enabled: boolean
      exclude_first_minutes: number
      exclude_last_minutes: number
    }
  }
  campaign_rules: {
    max_active_campaigns: number
    max_per_rolling_week: number
    week_start_day: 'monday' | 'sunday'
    overlap_check_enabled: boolean
  }
  structure: {
    legs: StrategyLeg[]
    protection_legs: StrategyLeg[]
  }
  selection_filters: {
    min_open_interest: number
    max_spread_pct_of_mid: number
    scoring_method: ScoringMethod
  }
  exit_rules: {
    profit_target_usd: number
    stop_loss_usd: number
    max_days_in_position: number
    hard_stop_conditions: HardStopCondition[]
  }
  execution_rules: {
    order_type: OrderType
    repricing: {
      enabled: boolean
      max_attempts: number
      interval_seconds: number
      step_pct_of_half_spread: number
      max_slippage_pct_from_first_mid: number
      fallback_on_max_attempts: 'cancel_and_block' | 'market' | 'cancel_and_alert'
    }
    opening_sequence: 'combo_first' | 'hedge_first' | 'simultaneous'
    margin_buffer_pct: number
    what_if_check_enabled: boolean
    gtc_target_order: {
      enabled: boolean
      submit_immediately_after_fill: boolean
    }
  }
  monitoring: {
    greeks_snapshot_interval_minutes: number
    risk_check_interval_minutes: number
  }
  notifications: {
    on_campaign_opened: boolean
    on_target_hit: boolean
    on_stop_loss_hit: boolean
    on_hard_stop_triggered: boolean
    on_max_days_close: boolean
    on_ivts_state_change: boolean
  }
}

export type StrategyDraft = DeepPartial<StrategyDefinition>
```

### sdf-validator.ts — Regole critiche

```typescript
export interface ValidationError {
  field: string      // dot-path: "entry_filters.ivts.suspend_threshold"
  message: string    // in italiano
  severity: 'error' | 'warning'
}

// Regole cross-field obbligatorie:
// - suspend_threshold > resume_threshold (stesso step → validazione immediata)
// - hard_stop.reference_leg_id esiste in structure.legs[].leg_id
// - target_delta in (0.01, 0.99)
// - profit_target_usd > 0
// - stop_loss_usd > 0
// - structure.legs.length >= 1

export function validateStep(step: number, draft: StrategyDraft): ValidationError[]
export function validateAll(draft: StrategyDraft): { valid: boolean; errors: ValidationError[]; warnings: ValidationError[] }
export function validateField(path: string, value: unknown, draft: StrategyDraft): ValidationError | null
```

### sdf-defaults.ts — Valori default

```typescript
export function createDefaultStrategy(): StrategyDraft
// Valori default per ogni campo (vedi architettura per valori esatti)
// Includi: schema_version=1, trading_mode default paper,
//          IVTS suspend=1.15, resume=1.10, ecc.

export function generateStrategyId(name: string): string
// "My Strategy Name!" → "my-strategy-name"
// Solo [a-z0-9-], max 50 chars

export function createDefaultLeg(index: number, action: 'buy'|'sell'): StrategyLeg
// Leg con valori tipici: delta=0.30 per sell, delta=0.16 per buy
```

## Test

- `TEST-SW-01-01`: `validateAll(emptyDraft)` → `valid=false`, `errors.length > 10`
- `TEST-SW-01-02`: `validateAll(completeDraft)` → `valid=true`, `errors.length === 0`
- `TEST-SW-01-03`: suspend=1.10, resume=1.10 → cross-field error su suspend_threshold
- `TEST-SW-01-04`: suspend=1.15, resume=1.10 → nessun errore cross-field
- `TEST-SW-01-05`: `target_delta = 1.5` → error "deve essere tra 0.01 e 0.99"
- `TEST-SW-01-06`: hard_stop con reference_leg_id non esistente → error descrittivo
- `TEST-SW-01-07`: `generateStrategyId("My Strat!")` → `"my-strat"`
- `TEST-SW-01-08`: `createDefaultStrategy()` → `schema_version === 1`
- `TEST-SW-01-09`: TypeScript strict compile (`tsc --strict`) → 0 errori
- `TEST-SW-01-10`: `createDefaultLeg(1, 'sell')` → `target_delta === 0.30`

## Done Criteria
- [ ] `tsc --strict` compila senza errori su tutti e 3 i file
- [ ] Tutti i test TEST-SW-01-XX passano
- [ ] No regression su test esistenti del progetto
- [ ] Ogni campo SDF v1 ha un tipo TypeScript corrispondente (confronto con C# T-15)
- [ ] Ogni campo obbligatorio ha un default in `createDefaultStrategy()`

## Stima
~1 giorno
