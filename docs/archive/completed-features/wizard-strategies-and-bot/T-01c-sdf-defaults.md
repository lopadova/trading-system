# T-SW-01c — SDF v1 Defaults and Utilities

## Obiettivo
Implementare utility per generazione valori default SDF v1, generazione automatica
strategy_id da nome, creazione leg con valori tipici, e funzioni helper per wizard.

## Dipendenze
- T-SW-01a (tipi TypeScript sdf-v1.ts)

## Files da Creare
- `dashboard/src/utils/sdf-defaults.ts`
- `dashboard/src/utils/sdf-defaults.test.ts`

## Files da Modificare
Nessuno.

## Implementazione

### sdf-defaults.ts — Defaults e utility

```typescript
import type {
  StrategyDraft,
  StrategyLeg,
  HardStopCondition,
  ChangelogEntry
} from '../types/sdf-v1'

/**
 * Genera uno strategy_id valido da un nome umano.
 * Rimuove caratteri speciali, converte in lowercase, max 50 chars.
 * 
 * @example
 * generateStrategyId("My Strategy Name!") // "my-strategy-name"
 * generateStrategyId("Iron Condor 45 DTE") // "iron-condor-45-dte"
 */
export function generateStrategyId(name: string): string {
  return name
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '') // Rimuovi caratteri speciali
    .trim()
    .replace(/\s+/g, '-')         // Spazi → trattini
    .replace(/-+/g, '-')          // Multipli trattini → singolo
    .slice(0, 50)                 // Max 50 caratteri
}

/**
 * Genera timestamp ISO 8601 UTC per created_at/updated_at.
 */
export function generateTimestamp(): string {
  return new Date().toISOString()
}

/**
 * Genera una versione iniziale per nuova strategia.
 */
export function generateInitialVersion(): string {
  return '1.0.0'
}

/**
 * Crea changelog entry iniziale.
 */
export function createInitialChangelog(name: string): ChangelogEntry[] {
  return [
    {
      version: '1.0.0',
      date: new Date().toISOString().split('T')[0], // YYYY-MM-DD
      notes: `Creazione iniziale strategia "${name}"`
    }
  ]
}

/**
 * Crea strategia con valori default completi.
 * Pronta per essere modificata nel wizard.
 */
export function createDefaultStrategy(partialName?: string): StrategyDraft {
  const name = partialName || 'Nuova Strategia'
  const strategyId = generateStrategyId(name)
  const now = generateTimestamp()

  return {
    $schema: 'https://padosoft.com/schemas/sdf-v1.json',
    strategy_id: strategyId,
    strategy_version: '1.0.0',
    schema_version: 1,
    name,
    author: 'Lorenzo Padovani',
    author_url: 'https://padosoft.com',
    license: 'Proprietary',
    description: '',
    tags: [],
    created_at: now,
    updated_at: now,
    enabled_default: false,
    changelog: createInitialChangelog(name),
    
    instrument: {
      type: 'options',
      underlying_symbol: 'SPX',
      underlying_sec_type: 'IND',
      underlying_exchange: 'CBOE',
      options_exchange: 'SMART',
      currency: 'USD',
      multiplier: 100,
      option_right: 'put'
    },

    entry_filters: {
      ivts: {
        enabled: true,
        formula: 'VIX/VIX1D',
        suspend_threshold: 1.15,
        resume_threshold: 1.10,
        staleness_max_minutes: 60,
        fallback_behavior: 'block'
      },
      market_hours_only: true,
      safe_execution_window: {
        enabled: true,
        exclude_first_minutes: 15,
        exclude_last_minutes: 30
      }
    },

    campaign_rules: {
      max_active_campaigns: 3,
      max_per_rolling_week: 5,
      week_start_day: 'monday',
      overlap_check_enabled: true
    },

    structure: {
      legs: [],
      protection_legs: []
    },

    selection_filters: {
      min_open_interest: 100,
      max_spread_pct_of_mid: 0.10,
      scoring_method: 'min_delta_distance'
    },

    exit_rules: {
      profit_target_usd: 500,
      stop_loss_usd: 1000,
      max_days_in_position: 45,
      hard_stop_conditions: []
    },

    execution_rules: {
      order_type: 'limit_mid',
      repricing: {
        enabled: true,
        max_attempts: 5,
        interval_seconds: 30,
        step_pct_of_half_spread: 0.10,
        max_slippage_pct_from_first_mid: 0.05,
        fallback_on_max_attempts: 'cancel_and_alert'
      },
      opening_sequence: 'combo_first',
      margin_buffer_pct: 0.10,
      what_if_check_enabled: true,
      gtc_target_order: {
        enabled: true,
        submit_immediately_after_fill: true
      }
    },

    monitoring: {
      greeks_snapshot_interval_minutes: 60,
      risk_check_interval_minutes: 15
    },

    notifications: {
      on_campaign_opened: true,
      on_target_hit: true,
      on_stop_loss_hit: true,
      on_hard_stop_triggered: true,
      on_max_days_close: true,
      on_ivts_state_change: true
    }
  }
}

/**
 * Crea un leg con valori tipici basati su action.
 * 
 * @param index - Indice del leg (per generare leg_id)
 * @param action - 'buy' o 'sell'
 * @param right - 'put' o 'call' (default 'put')
 * 
 * Valori tipici:
 * - Sell put: delta 0.30, DTE 45
 * - Buy put: delta 0.16, DTE 45 (protezione)
 * - Sell call: delta 0.30, DTE 45
 * - Buy call: delta 0.16, DTE 45 (protezione)
 */
export function createDefaultLeg(
  index: number,
  action: 'buy' | 'sell',
  right: 'put' | 'call' = 'put'
): StrategyLeg {
  // Valori tipici per delta in base ad action
  const targetDelta = action === 'sell' ? 0.30 : 0.16
  const role = action === 'sell' ? 'income' : 'protection'
  const orderGroup = action === 'sell' ? 'combo' : 'standalone'

  return {
    leg_id: `leg-${index + 1}`,
    action,
    right,
    target_dte: 45,
    dte_tolerance: 3,
    target_delta: targetDelta,
    delta_tolerance: 0.05,
    quantity: 1,
    settlement_preference: 'PM',
    exclude_expiry_within_days: 0,
    role,
    order_group,
    open_sequence: index === 0 ? '1' : undefined
  }
}

/**
 * Crea hard stop condition con valori default.
 */
export function createDefaultHardStop(
  index: number,
  type: 'underlying_vs_leg_strike' | 'portfolio_greek' | 'pnl_threshold' = 'pnl_threshold'
): HardStopCondition {
  const defaults: Record<string, Partial<HardStopCondition>> = {
    pnl_threshold: {
      operator: 'lt',
      threshold: -1500,
      severity: 'critical',
      close_sequence: 'all_legs'
    },
    portfolio_greek: {
      greek: 'delta',
      operator: 'gt',
      threshold: 0.70,
      severity: 'high',
      close_sequence: 'all_legs'
    },
    underlying_vs_leg_strike: {
      operator: 'lte',
      threshold: 0.95,
      severity: 'critical',
      close_sequence: 'all_legs'
    }
  }

  return {
    condition_id: `stop-${index + 1}`,
    type,
    ...defaults[type]
  } as HardStopCondition
}

/**
 * Clona una strategia aggiornando ID, version, timestamp.
 * Utile per "duplicate strategy" feature.
 */
export function cloneStrategy(
  original: StrategyDraft,
  newName: string
): StrategyDraft {
  const strategyId = generateStrategyId(newName)
  const now = generateTimestamp()

  return {
    ...original,
    strategy_id: strategyId,
    name: newName,
    strategy_version: '1.0.0',
    created_at: now,
    updated_at: now,
    changelog: createInitialChangelog(newName)
  }
}

/**
 * Incrementa version number seguendo semver.
 * 
 * @param currentVersion - Versione corrente (es. "1.2.3")
 * @param level - Livello da incrementare: 'major' | 'minor' | 'patch'
 */
export function incrementVersion(
  currentVersion: string,
  level: 'major' | 'minor' | 'patch'
): string {
  const [major, minor, patch] = currentVersion.split('.').map(Number)

  switch (level) {
    case 'major':
      return `${major + 1}.0.0`
    case 'minor':
      return `${major}.${minor + 1}.0`
    case 'patch':
      return `${major}.${minor}.${patch + 1}`
    default:
      return currentVersion
  }
}
```

## Test

### sdf-defaults.test.ts

- `TEST-SW-01c-01`: `generateStrategyId("My Strategy!")` → `"my-strategy"`
- `TEST-SW-01c-02`: `generateStrategyId("Iron Condor 45 DTE")` → `"iron-condor-45-dte"`
- `TEST-SW-01c-03`: `generateStrategyId("A".repeat(100))` → max 50 chars
- `TEST-SW-01c-04`: `generateStrategyId("Test---Multiple---Dashes")` → `"test-multiple-dashes"`
- `TEST-SW-01c-05`: `createDefaultStrategy()` → `schema_version === 1`
- `TEST-SW-01c-06`: `createDefaultStrategy()` → tutti i campi required presenti
- `TEST-SW-01c-07`: `createDefaultStrategy("My Strat")` → `name === "My Strat"`
- `TEST-SW-01c-08`: `createDefaultLeg(0, 'sell')` → `target_delta === 0.30`
- `TEST-SW-01c-09`: `createDefaultLeg(1, 'buy')` → `target_delta === 0.16`
- `TEST-SW-01c-10`: `createDefaultLeg(0, 'sell', 'call')` → `right === 'call'`
- `TEST-SW-01c-11`: `createDefaultHardStop(0, 'pnl_threshold')` → threshold < 0
- `TEST-SW-01c-12`: `createDefaultHardStop(1, 'portfolio_greek')` → `greek === 'delta'`
- `TEST-SW-01c-13`: `cloneStrategy(orig, "New Name")` → nuovo strategy_id generato
- `TEST-SW-01c-14`: `cloneStrategy(orig, "New Name")` → `version === "1.0.0"`
- `TEST-SW-01c-15`: `incrementVersion("1.2.3", "major")` → `"2.0.0"`
- `TEST-SW-01c-16`: `incrementVersion("1.2.3", "minor")` → `"1.3.0"`
- `TEST-SW-01c-17`: `incrementVersion("1.2.3", "patch")` → `"1.2.4"`
- `TEST-SW-01c-18`: `generateTimestamp()` → formato ISO 8601 valido
- `TEST-SW-01c-19`: `createInitialChangelog("Test")` → array con 1 entry
- `TEST-SW-01c-20`: `createInitialChangelog("Test")` → version "1.0.0"

## Done Criteria

- [ ] `tsc --strict` compila senza errori
- [ ] Tutti i test TEST-SW-01c-XX passano
- [ ] No regression su test esistenti
- [ ] `createDefaultStrategy()` passa `validateAll()` del validator
- [ ] Ogni function ha JSDoc con @example
- [ ] generateStrategyId testato con casi edge (unicode, lunghezza, caratteri speciali)

## Stima

~4 ore
