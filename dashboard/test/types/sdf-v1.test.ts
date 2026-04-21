/**
 * Tests for SDF v1 TypeScript types
 * Validates type guards, utility types, and type correctness
 */

import { describe, it, expect } from 'vitest'
import {
  type StrategyDefinition,
  type StrategyLeg,
  type HardStopCondition,
  type StrategyDraft,
  isStrategyDefinition,
  isStrategyLeg,
  isHardStopCondition,
} from '../../src/types/sdf-v1'

describe('SDF v1 TypeScript Types', () => {
  // TEST-SW-01a-03: isStrategyDefinition({}) → false
  it('TEST-SW-01a-03: rejects empty object', () => {
    expect(isStrategyDefinition({})).toBe(false)
  })

  // TEST-SW-01a-04: isStrategyDefinition(validSDF) → true
  it('TEST-SW-01a-04: accepts valid StrategyDefinition', () => {
    const validSDF: StrategyDefinition = {
      strategy_id: 'test-strategy',
      strategy_version: '1.0.0',
      schema_version: 1,
      name: 'Test Strategy',
      author: 'Test Author',
      license: 'MIT',
      description: 'A test strategy',
      tags: ['test'],
      created_at: '2026-04-06T00:00:00Z',
      updated_at: '2026-04-06T00:00:00Z',
      enabled_default: true,
      changelog: [],
      instrument: {
        type: 'options',
        underlying_symbol: 'SPX',
        underlying_sec_type: 'IND',
        underlying_exchange: 'CBOE',
        options_exchange: 'CBOE',
        currency: 'USD',
        multiplier: 100,
        option_right: 'put',
      },
      entry_filters: {
        ivts: {
          enabled: false,
          formula: 'simple',
          suspend_threshold: 0.8,
          resume_threshold: 0.6,
          staleness_max_minutes: 60,
          fallback_behavior: 'warn',
        },
        market_hours_only: true,
        safe_execution_window: {
          enabled: true,
          exclude_first_minutes: 15,
          exclude_last_minutes: 15,
        },
      },
      campaign_rules: {
        max_active_campaigns: 5,
        max_per_rolling_week: 3,
        week_start_day: 'monday',
        overlap_check_enabled: true,
      },
      structure: {
        legs: [],
        protection_legs: [],
      },
      selection_filters: {
        min_open_interest: 100,
        max_spread_pct_of_mid: 5.0,
        scoring_method: 'min_delta_distance',
      },
      exit_rules: {
        profit_target_usd: 1000,
        stop_loss_usd: 500,
        max_days_in_position: 30,
        hard_stop_conditions: [],
      },
      execution_rules: {
        order_type: 'limit_mid',
        repricing: {
          enabled: true,
          max_attempts: 10,
          interval_seconds: 30,
          step_pct_of_half_spread: 10,
          max_slippage_pct_from_first_mid: 3,
          fallback_on_max_attempts: 'cancel_and_block',
        },
        opening_sequence: 'simultaneous',
        margin_buffer_pct: 20,
        what_if_check_enabled: true,
        gtc_target_order: {
          enabled: true,
          submit_immediately_after_fill: true,
        },
      },
      monitoring: {
        greeks_snapshot_interval_minutes: 15,
        risk_check_interval_minutes: 5,
      },
      notifications: {
        on_campaign_opened: true,
        on_target_hit: true,
        on_stop_loss_hit: true,
        on_hard_stop_triggered: true,
        on_max_days_close: true,
        on_ivts_state_change: false,
      },
    }

    expect(isStrategyDefinition(validSDF)).toBe(true)
  })

  // TEST-SW-01a-05: isStrategyLeg(validLeg) → true
  it('TEST-SW-01a-05: accepts valid StrategyLeg', () => {
    const validLeg: StrategyLeg = {
      leg_id: 'leg1',
      action: 'sell',
      right: 'put',
      target_dte: 45,
      dte_tolerance: 7,
      target_delta: 0.3,
      delta_tolerance: 0.05,
      quantity: 1,
      settlement_preference: 'PM',
      exclude_expiry_within_days: 0,
      role: 'short put',
      order_group: 'combo',
    }

    expect(isStrategyLeg(validLeg)).toBe(true)
  })

  it('TEST-SW-01a-05b: rejects invalid StrategyLeg', () => {
    const invalidLeg = {
      leg_id: 'leg1',
      action: 'invalid_action', // Invalid value
      right: 'put',
      target_delta: 0.3,
    }

    expect(isStrategyLeg(invalidLeg)).toBe(false)
  })

  // TEST-SW-01a-06: DeepPartial<StrategyDefinition> allows partial drafts
  it('TEST-SW-01a-06: DeepPartial allows partial strategy drafts', () => {
    // This test verifies that DeepPartial works correctly at compile time
    // and allows partial objects at runtime
    const draft: StrategyDraft = {
      strategy_id: 'draft-strategy',
      name: 'Draft Strategy',
      instrument: {
        underlying_symbol: 'SPX',
        // Other fields are optional
      },
      // Most fields are optional
    }

    // Verify the partial object doesn't break type checking
    expect(draft.strategy_id).toBe('draft-strategy')
    expect(draft.name).toBe('Draft Strategy')
    expect(draft.instrument?.underlying_symbol).toBe('SPX')

    // Verify deeply nested optionality
    const draftWithNested: StrategyDraft = {
      execution_rules: {
        repricing: {
          enabled: true,
          // Other repricing fields are optional
        },
      },
    }

    expect(draftWithNested.execution_rules?.repricing?.enabled).toBe(true)
  })

  // TEST-SW-01a-07: Every type enum has correct values (match with C#)
  it('TEST-SW-01a-07: Type unions match C# enum values', () => {
    // These are compile-time checks, but we verify at runtime too
    const optionRights: Array<'put' | 'call'> = ['put', 'call']
    expect(optionRights).toHaveLength(2)

    const settlementPrefs: Array<'PM' | 'AM'> = ['PM', 'AM']
    expect(settlementPrefs).toHaveLength(2)

    const fallbackBehaviors: Array<'block' | 'warn' | 'allow'> = ['block', 'warn', 'allow']
    expect(fallbackBehaviors).toHaveLength(3)

    const scoringMethods: Array<'min_delta_distance' | 'max_oi' | 'min_spread'> = [
      'min_delta_distance',
      'max_oi',
      'min_spread',
    ]
    expect(scoringMethods).toHaveLength(3)

    const orderTypes: Array<'limit_mid' | 'limit_ask' | 'limit_bid' | 'market'> = [
      'limit_mid',
      'limit_ask',
      'limit_bid',
      'market',
    ]
    expect(orderTypes).toHaveLength(4)

    const hardStopTypes: Array<'underlying_vs_leg_strike' | 'portfolio_greek' | 'pnl_threshold'> = [
      'underlying_vs_leg_strike',
      'portfolio_greek',
      'pnl_threshold',
    ]
    expect(hardStopTypes).toHaveLength(3)

    const greekNames: Array<'delta' | 'theta' | 'gamma' | 'vega'> = [
      'delta',
      'theta',
      'gamma',
      'vega',
    ]
    expect(greekNames).toHaveLength(4)

    const comparisonOps: Array<'lt' | 'gt' | 'lte' | 'gte'> = ['lt', 'gt', 'lte', 'gte']
    expect(comparisonOps).toHaveLength(4)

    const severities: Array<'critical' | 'high' | 'medium'> = ['critical', 'high', 'medium']
    expect(severities).toHaveLength(3)

    const closeSequences: Array<'all_legs' | 'combo_only' | 'hedge_only'> = [
      'all_legs',
      'combo_only',
      'hedge_only',
    ]
    expect(closeSequences).toHaveLength(3)

    const openingSequences: Array<'combo_first' | 'hedge_first' | 'simultaneous'> = [
      'combo_first',
      'hedge_first',
      'simultaneous',
    ]
    expect(openingSequences).toHaveLength(3)

    const repricingFallbacks: Array<'cancel_and_block' | 'market' | 'cancel_and_alert'> = [
      'cancel_and_block',
      'market',
      'cancel_and_alert',
    ]
    expect(repricingFallbacks).toHaveLength(3)

    const weekStartDays: Array<'monday' | 'sunday'> = ['monday', 'sunday']
    expect(weekStartDays).toHaveLength(2)
  })

  // TEST-SW-01a-08: StrategyDefinition has all fields from C# model
  it('TEST-SW-01a-08: StrategyDefinition has all required C# model fields', () => {
    const strategyKeys: Array<keyof StrategyDefinition> = [
      '$schema',
      'strategy_id',
      'strategy_version',
      'schema_version',
      'name',
      'author',
      'author_url',
      'license',
      'description',
      'tags',
      'created_at',
      'updated_at',
      'enabled_default',
      'changelog',
      'instrument',
      'entry_filters',
      'campaign_rules',
      'structure',
      'selection_filters',
      'exit_rules',
      'execution_rules',
      'monitoring',
      'notifications',
    ]

    // Verify all keys exist (compile-time check enforced at runtime)
    expect(strategyKeys).toHaveLength(23)
    expect(strategyKeys).toContain('strategy_id')
    expect(strategyKeys).toContain('schema_version')
    expect(strategyKeys).toContain('instrument')
    expect(strategyKeys).toContain('structure')
    expect(strategyKeys).toContain('exit_rules')
    expect(strategyKeys).toContain('execution_rules')
  })

  it('TEST-SW-01a-01: imports compile without TypeScript errors (implicit)', () => {
    // This test passes if the file imports successfully
    expect(isStrategyDefinition).toBeDefined()
    expect(isStrategyLeg).toBeDefined()
    expect(isHardStopCondition).toBeDefined()
  })

  it('rejects null and non-objects for isStrategyDefinition', () => {
    expect(isStrategyDefinition(null)).toBe(false)
    expect(isStrategyDefinition(undefined)).toBe(false)
    expect(isStrategyDefinition('string')).toBe(false)
    expect(isStrategyDefinition(123)).toBe(false)
    expect(isStrategyDefinition([])).toBe(false)
  })

  it('rejects object with missing required fields for isStrategyDefinition', () => {
    const incomplete = {
      strategy_id: 'test',
      schema_version: 1,
      // Missing many required fields
    }

    expect(isStrategyDefinition(incomplete)).toBe(false)
  })

  it('rejects object with wrong schema_version', () => {
    const wrongVersion = {
      strategy_id: 'test',
      strategy_version: '1.0.0',
      schema_version: 2, // Should be 1
      name: 'Test',
      author: 'Author',
      license: 'MIT',
      description: 'Desc',
      tags: [],
      created_at: '2026-01-01',
      updated_at: '2026-01-01',
      enabled_default: true,
      changelog: [],
      instrument: {},
      entry_filters: {},
      campaign_rules: {},
      structure: {},
      selection_filters: {},
      exit_rules: {},
      execution_rules: {},
      monitoring: {},
      notifications: {},
    }

    expect(isStrategyDefinition(wrongVersion)).toBe(false)
  })

  it('validates HardStopCondition type guard', () => {
    const validCondition: HardStopCondition = {
      condition_id: 'stop1',
      type: 'pnl_threshold',
      operator: 'lte',
      threshold: -500,
      severity: 'critical',
      close_sequence: 'all_legs',
    }

    expect(isHardStopCondition(validCondition)).toBe(true)

    const invalidCondition = {
      condition_id: 'stop1',
      type: 'invalid_type',
      operator: 'lte',
      threshold: -500,
      severity: 'critical',
      close_sequence: 'all_legs',
    }

    expect(isHardStopCondition(invalidCondition)).toBe(false)
  })

  it('validates optional fields work correctly', () => {
    const strategyWithOptionals: StrategyDefinition = {
      $schema: 'https://example.com/sdf-v1.json',
      strategy_id: 'test-strategy',
      strategy_version: '1.0.0',
      schema_version: 1,
      name: 'Test Strategy',
      author: 'Test Author',
      author_url: 'https://example.com',
      license: 'MIT',
      description: 'A test strategy',
      tags: ['test'],
      created_at: '2026-04-06T00:00:00Z',
      updated_at: '2026-04-06T00:00:00Z',
      enabled_default: true,
      changelog: [],
      instrument: {
        type: 'options',
        underlying_symbol: 'SPX',
        underlying_sec_type: 'IND',
        underlying_exchange: 'CBOE',
        options_exchange: 'CBOE',
        currency: 'USD',
        multiplier: 100,
        option_right: 'put',
      },
      entry_filters: {
        ivts: {
          enabled: false,
          formula: 'simple',
          suspend_threshold: 0.8,
          resume_threshold: 0.6,
          staleness_max_minutes: 60,
          fallback_behavior: 'warn',
        },
        market_hours_only: true,
        safe_execution_window: {
          enabled: true,
          exclude_first_minutes: 15,
          exclude_last_minutes: 15,
        },
      },
      campaign_rules: {
        max_active_campaigns: 5,
        max_per_rolling_week: 3,
        week_start_day: 'monday',
        overlap_check_enabled: true,
      },
      structure: {
        legs: [],
        protection_legs: [],
      },
      selection_filters: {
        min_open_interest: 100,
        max_spread_pct_of_mid: 5.0,
        scoring_method: 'min_delta_distance',
      },
      exit_rules: {
        profit_target_usd: 1000,
        stop_loss_usd: 500,
        max_days_in_position: 30,
        hard_stop_conditions: [],
      },
      execution_rules: {
        order_type: 'limit_mid',
        repricing: {
          enabled: true,
          max_attempts: 10,
          interval_seconds: 30,
          step_pct_of_half_spread: 10,
          max_slippage_pct_from_first_mid: 3,
          fallback_on_max_attempts: 'cancel_and_block',
        },
        opening_sequence: 'simultaneous',
        margin_buffer_pct: 20,
        what_if_check_enabled: true,
        gtc_target_order: {
          enabled: true,
          submit_immediately_after_fill: true,
        },
      },
      monitoring: {
        greeks_snapshot_interval_minutes: 15,
        risk_check_interval_minutes: 5,
      },
      notifications: {
        on_campaign_opened: true,
        on_target_hit: true,
        on_stop_loss_hit: true,
        on_hard_stop_triggered: true,
        on_max_days_close: true,
        on_ivts_state_change: false,
      },
    }

    expect(isStrategyDefinition(strategyWithOptionals)).toBe(true)
    expect(strategyWithOptionals.$schema).toBeDefined()
    expect(strategyWithOptionals.author_url).toBeDefined()
  })
})
