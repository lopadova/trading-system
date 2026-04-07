/**
 * SDF v1 — Strategy Definition Format TypeScript Types
 *
 * These types mirror 1:1 the C# models for strategy definitions.
 * This is the foundation for the strategy wizard and ensures type-safe
 * strategy creation, validation, and storage.
 *
 * @module sdf-v1
 */

// ============================================================================
// ENUMS (Type Unions)
// ============================================================================

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

// ============================================================================
// LEG DEFINITION
// ============================================================================

/**
 * Defines a single option leg within a strategy structure.
 * Each leg specifies entry criteria (DTE, delta) and execution parameters.
 */
export interface StrategyLeg {
  /** Unique identifier for this leg within the strategy */
  leg_id: string
  /** Buy to open or sell to open */
  action: 'buy' | 'sell'
  /** Put or call option */
  right: OptionRight
  /** Target days to expiration */
  target_dte: number
  /** Acceptable deviation from target DTE */
  dte_tolerance: number
  /** Target delta value (e.g., 0.30 for 30 delta) */
  target_delta: number
  /** Acceptable deviation from target delta */
  delta_tolerance: number
  /** Number of contracts */
  quantity: number
  /** Preference for PM-settled or AM-settled options */
  settlement_preference: SettlementPreference
  /** Exclude expirations within this many days from today */
  exclude_expiry_within_days: number
  /** Human-readable role description (e.g., "short put", "long call hedge") */
  role: string
  /** Whether this leg trades as part of a combo or standalone */
  order_group: 'combo' | 'standalone'
  /** Optional sequence identifier for opening order execution */
  open_sequence?: string
}

// ============================================================================
// HARD STOP CONDITION
// ============================================================================

/**
 * Defines an automatic exit trigger based on portfolio risk or P&L.
 * Hard stops execute immediately when condition is met.
 */
export interface HardStopCondition {
  /** Unique identifier for this condition */
  condition_id: string
  /** Type of stop condition */
  type: HardStopType
  /** Reference leg ID (required for underlying_vs_leg_strike type) */
  reference_leg_id?: string
  /** Greek to monitor (required for portfolio_greek type) */
  greek?: GreekName
  /** Comparison operator (lt, gt, lte, gte) */
  operator: ComparisonOperator
  /** Threshold value that triggers the stop */
  threshold: number
  /** Urgency level of this stop */
  severity: Severity
  /** How to close the position when triggered */
  close_sequence: CloseSequence
}

// ============================================================================
// CHANGELOG ENTRY
// ============================================================================

/**
 * Documents changes to the strategy definition over time.
 */
export interface ChangelogEntry {
  /** Semantic version (e.g., "1.0.0", "1.1.0") */
  version: string
  /** ISO 8601 date string */
  date: string
  /** Description of changes in this version */
  notes: string
}

// ============================================================================
// INSTRUMENT CONFIGURATION
// ============================================================================

/**
 * Defines the underlying instrument and options market details.
 */
export interface InstrumentConfig {
  /** Always "options" for SDF v1 */
  type: 'options'
  /** Underlying symbol (e.g., "SPX", "SPY") */
  underlying_symbol: string
  /** Underlying security type */
  underlying_sec_type: 'IND' | 'STK' | 'ETF'
  /** Primary exchange for the underlying */
  underlying_exchange: string
  /** Options exchange to trade on */
  options_exchange: string
  /** Currency for the instrument */
  currency: string
  /** Contract multiplier (e.g., 100 for SPX) */
  multiplier: number
  /** Option right filter (can restrict to only puts or only calls) */
  option_right: OptionRight
}

// ============================================================================
// IVTS FILTER CONFIGURATION
// ============================================================================

/**
 * Implied volatility term structure filter.
 * Suspends entry when IVTS signal exceeds threshold.
 */
export interface IvtsFilter {
  /** Whether IVTS filtering is active */
  enabled: boolean
  /** Formula name or identifier for IVTS calculation */
  formula: string
  /** IVTS value above which entry is suspended */
  suspend_threshold: number
  /** IVTS value below which entry resumes */
  resume_threshold: number
  /** Max age of IVTS data in minutes before considered stale */
  staleness_max_minutes: number
  /** What to do when IVTS data is stale or unavailable */
  fallback_behavior: FallbackBehavior
}

// ============================================================================
// SAFE EXECUTION WINDOW
// ============================================================================

/**
 * Defines time-of-day restrictions to avoid illiquid markets.
 */
export interface SafeExecutionWindow {
  /** Whether execution window filtering is active */
  enabled: boolean
  /** Minutes to exclude after market open */
  exclude_first_minutes: number
  /** Minutes to exclude before market close */
  exclude_last_minutes: number
}

// ============================================================================
// ENTRY FILTERS
// ============================================================================

/**
 * Aggregates all entry-time filters and checks.
 */
export interface EntryFilters {
  /** IVTS filter configuration */
  ivts: IvtsFilter
  /** Restrict entries to regular market hours only */
  market_hours_only: boolean
  /** Safe execution window to avoid illiquid periods */
  safe_execution_window: SafeExecutionWindow
}

// ============================================================================
// CAMPAIGN RULES
// ============================================================================

/**
 * Defines constraints on campaign opening and concurrency.
 */
export interface CampaignRules {
  /** Maximum number of active campaigns allowed at once */
  max_active_campaigns: number
  /** Maximum number of campaigns to open per rolling week */
  max_per_rolling_week: number
  /** Day that defines the start of a week for counting */
  week_start_day: WeekStartDay
  /** Whether to check for DTE overlap before opening new campaign */
  overlap_check_enabled: boolean
}

// ============================================================================
// STRUCTURE
// ============================================================================

/**
 * Defines the complete leg structure of the strategy.
 */
export interface StrategyStructure {
  /** Primary income/directional legs */
  legs: StrategyLeg[]
  /** Optional protection or hedge legs */
  protection_legs: StrategyLeg[]
}

// ============================================================================
// SELECTION FILTERS
// ============================================================================

/**
 * Filters and scoring for option chain selection.
 */
export interface SelectionFilters {
  /** Minimum open interest required for each option */
  min_open_interest: number
  /** Maximum bid-ask spread as percentage of mid price */
  max_spread_pct_of_mid: number
  /** Method to score and rank candidate option chains */
  scoring_method: ScoringMethod
}

// ============================================================================
// EXIT RULES
// ============================================================================

/**
 * Defines profit targets, stop losses, and automatic exit conditions.
 */
export interface ExitRules {
  /** USD profit at which to close position (0 = disabled) */
  profit_target_usd: number
  /** USD loss at which to close position (0 = disabled) */
  stop_loss_usd: number
  /** Maximum days to hold position (0 = unlimited) */
  max_days_in_position: number
  /** Array of hard stop conditions for risk management */
  hard_stop_conditions: HardStopCondition[]
}

// ============================================================================
// REPRICING CONFIGURATION
// ============================================================================

/**
 * Automatic order repricing logic for unfilled orders.
 */
export interface RepricingConfig {
  /** Whether repricing is enabled */
  enabled: boolean
  /** Maximum number of repricing attempts before fallback */
  max_attempts: number
  /** Seconds to wait between repricing attempts */
  interval_seconds: number
  /** Step size as percentage of half spread to adjust price */
  step_pct_of_half_spread: number
  /** Maximum total slippage from initial mid price */
  max_slippage_pct_from_first_mid: number
  /** Action to take if max attempts reached without fill */
  fallback_on_max_attempts: RepricingFallback
}

// ============================================================================
// GTC TARGET ORDER CONFIGURATION
// ============================================================================

/**
 * Good-til-cancelled profit target order configuration.
 */
export interface GtcTargetOrder {
  /** Whether to submit GTC profit target order */
  enabled: boolean
  /** Submit target order immediately after entry fill */
  submit_immediately_after_fill: boolean
}

// ============================================================================
// EXECUTION RULES
// ============================================================================

/**
 * Order execution and fill management rules.
 */
export interface ExecutionRules {
  /** Default order type for entries */
  order_type: OrderType
  /** Repricing configuration for unfilled orders */
  repricing: RepricingConfig
  /** Sequence for opening combo vs standalone legs */
  opening_sequence: OpeningSequence
  /** Margin buffer percentage required before opening */
  margin_buffer_pct: number
  /** Whether to run IBKR "what-if" check before order submission */
  what_if_check_enabled: boolean
  /** GTC target order configuration */
  gtc_target_order: GtcTargetOrder
}

// ============================================================================
// MONITORING CONFIGURATION
// ============================================================================

/**
 * Defines monitoring intervals for position Greeks and risk checks.
 */
export interface MonitoringConfig {
  /** How often to snapshot portfolio Greeks (minutes) */
  greeks_snapshot_interval_minutes: number
  /** How often to run risk checks (minutes) */
  risk_check_interval_minutes: number
}

// ============================================================================
// NOTIFICATIONS CONFIGURATION
// ============================================================================

/**
 * Defines which events trigger user notifications.
 */
export interface NotificationsConfig {
  /** Send notification when campaign opens */
  on_campaign_opened: boolean
  /** Send notification when profit target hit */
  on_target_hit: boolean
  /** Send notification when stop loss hit */
  on_stop_loss_hit: boolean
  /** Send notification when hard stop triggered */
  on_hard_stop_triggered: boolean
  /** Send notification when position closed due to max days */
  on_max_days_close: boolean
  /** Send notification when IVTS state changes (suspend/resume) */
  on_ivts_state_change: boolean
}

// ============================================================================
// COMPLETE STRATEGY DEFINITION (SDF v1)
// ============================================================================

/**
 * Complete strategy definition in SDF v1 format.
 * This is the root type for all strategy files.
 */
export interface StrategyDefinition {
  /** Optional JSON Schema reference (for validation) */
  $schema?: string
  /** Unique identifier for this strategy (kebab-case recommended) */
  strategy_id: string
  /** Current version of this strategy definition (semantic versioning) */
  strategy_version: string
  /** Schema version (always 1 for SDF v1) */
  schema_version: 1
  /** Human-readable strategy name */
  name: string
  /** Strategy author name or organization */
  author: string
  /** Optional URL to author's website or profile */
  author_url?: string
  /** License identifier (e.g., "MIT", "Proprietary") */
  license: string
  /** Detailed strategy description and rationale */
  description: string
  /** Tags for categorization and search */
  tags: string[]
  /** ISO 8601 timestamp of strategy creation */
  created_at: string
  /** ISO 8601 timestamp of last update */
  updated_at: string
  /** Whether this strategy is enabled by default when loaded */
  enabled_default: boolean
  /** Version history of this strategy */
  changelog: ChangelogEntry[]
  /** Instrument and market configuration */
  instrument: InstrumentConfig
  /** Entry filters and conditions */
  entry_filters: EntryFilters
  /** Campaign opening and concurrency rules */
  campaign_rules: CampaignRules
  /** Strategy leg structure */
  structure: StrategyStructure
  /** Option chain selection filters */
  selection_filters: SelectionFilters
  /** Exit rules and profit/loss targets */
  exit_rules: ExitRules
  /** Order execution and fill rules */
  execution_rules: ExecutionRules
  /** Monitoring intervals */
  monitoring: MonitoringConfig
  /** Notification preferences */
  notifications: NotificationsConfig
}

// ============================================================================
// UTILITY TYPES
// ============================================================================

/**
 * Utility type to make all properties optional recursively.
 * Useful for draft strategies in the wizard.
 */
export type DeepPartial<T> = T extends object
  ? {
      [P in keyof T]?: DeepPartial<T[P]>
    }
  : T

/**
 * Type alias for strategy drafts (partially filled strategies).
 * Used during wizard flow before strategy is complete.
 */
export type StrategyDraft = DeepPartial<StrategyDefinition>

// ============================================================================
// TYPE GUARDS
// ============================================================================

/**
 * Type guard to check if an unknown object is a valid StrategyDefinition.
 * Performs runtime validation of required fields.
 *
 * @param obj - Object to validate
 * @returns True if obj is a StrategyDefinition
 */
export function isStrategyDefinition(obj: unknown): obj is StrategyDefinition {
  if (typeof obj !== 'object' || obj === null) return false
  const s = obj as Record<string, unknown>
  return (
    typeof s.strategy_id === 'string' &&
    typeof s.strategy_version === 'string' &&
    typeof s.schema_version === 'number' &&
    s.schema_version === 1 &&
    typeof s.name === 'string' &&
    typeof s.author === 'string' &&
    typeof s.license === 'string' &&
    typeof s.description === 'string' &&
    Array.isArray(s.tags) &&
    typeof s.created_at === 'string' &&
    typeof s.updated_at === 'string' &&
    typeof s.enabled_default === 'boolean' &&
    Array.isArray(s.changelog) &&
    typeof s.instrument === 'object' &&
    s.instrument !== null &&
    typeof s.entry_filters === 'object' &&
    s.entry_filters !== null &&
    typeof s.campaign_rules === 'object' &&
    s.campaign_rules !== null &&
    typeof s.structure === 'object' &&
    s.structure !== null &&
    typeof s.selection_filters === 'object' &&
    s.selection_filters !== null &&
    typeof s.exit_rules === 'object' &&
    s.exit_rules !== null &&
    typeof s.execution_rules === 'object' &&
    s.execution_rules !== null &&
    typeof s.monitoring === 'object' &&
    s.monitoring !== null &&
    typeof s.notifications === 'object' &&
    s.notifications !== null
  )
}

/**
 * Type guard to check if an unknown object is a valid StrategyLeg.
 *
 * @param obj - Object to validate
 * @returns True if obj is a StrategyLeg
 */
export function isStrategyLeg(obj: unknown): obj is StrategyLeg {
  if (typeof obj !== 'object' || obj === null) return false
  const l = obj as Record<string, unknown>
  return (
    typeof l.leg_id === 'string' &&
    (l.action === 'buy' || l.action === 'sell') &&
    (l.right === 'put' || l.right === 'call') &&
    typeof l.target_dte === 'number' &&
    typeof l.dte_tolerance === 'number' &&
    typeof l.target_delta === 'number' &&
    typeof l.delta_tolerance === 'number' &&
    typeof l.quantity === 'number' &&
    (l.settlement_preference === 'PM' || l.settlement_preference === 'AM') &&
    typeof l.exclude_expiry_within_days === 'number' &&
    typeof l.role === 'string' &&
    (l.order_group === 'combo' || l.order_group === 'standalone')
  )
}

/**
 * Type guard to check if an unknown object is a valid HardStopCondition.
 *
 * @param obj - Object to validate
 * @returns True if obj is a HardStopCondition
 */
export function isHardStopCondition(obj: unknown): obj is HardStopCondition {
  if (typeof obj !== 'object' || obj === null) return false
  const h = obj as Record<string, unknown>
  return (
    typeof h.condition_id === 'string' &&
    (h.type === 'underlying_vs_leg_strike' ||
      h.type === 'portfolio_greek' ||
      h.type === 'pnl_threshold') &&
    (h.operator === 'lt' || h.operator === 'gt' || h.operator === 'lte' || h.operator === 'gte') &&
    typeof h.threshold === 'number' &&
    (h.severity === 'critical' || h.severity === 'high' || h.severity === 'medium') &&
    (h.close_sequence === 'all_legs' ||
      h.close_sequence === 'combo_only' ||
      h.close_sequence === 'hedge_only')
  )
}
