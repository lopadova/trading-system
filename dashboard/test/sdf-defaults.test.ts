/**
 * SDF Defaults Tests
 * Covers all 20 test cases specified in T-SW-01c
 */

import { describe, it, expect } from 'vitest'
import {
  generateStrategyId,
  generateTimestamp,
  generateInitialVersion,
  createInitialChangelog,
  createDefaultStrategy,
  createDefaultLeg,
  createDefaultHardStop,
  cloneStrategy,
  incrementVersion,
} from '@/utils/sdf-defaults'
import { validateAll } from '@/utils/sdf-validator'
import type { StrategyDraft } from '@/types/sdf-v1'

describe('sdf-defaults', () => {
  // ============================================================================
  // generateStrategyId Tests
  // ============================================================================

  it('TEST-SW-01c-01: generateStrategyId("My Strategy!") → "my-strategy"', () => {
    const result = generateStrategyId('My Strategy!')
    expect(result).toBe('my-strategy')
  })

  it('TEST-SW-01c-02: generateStrategyId("Iron Condor 45 DTE") → "iron-condor-45-dte"', () => {
    const result = generateStrategyId('Iron Condor 45 DTE')
    expect(result).toBe('iron-condor-45-dte')
  })

  it('TEST-SW-01c-03: generateStrategyId("A".repeat(100)) → max 50 chars', () => {
    const longName = 'A'.repeat(100)
    const result = generateStrategyId(longName)
    expect(result.length).toBeLessThanOrEqual(50)
    expect(result).toBe('a'.repeat(50))
  })

  it('TEST-SW-01c-04: generateStrategyId("Test---Multiple---Dashes") → "test-multiple-dashes"', () => {
    const result = generateStrategyId('Test---Multiple---Dashes')
    expect(result).toBe('test-multiple-dashes')
  })

  // ============================================================================
  // createDefaultStrategy Tests
  // ============================================================================

  it('TEST-SW-01c-05: createDefaultStrategy() → schema_version === 1', () => {
    const strategy = createDefaultStrategy()
    expect(strategy.schema_version).toBe(1)
  })

  it('TEST-SW-01c-06: createDefaultStrategy() → tutti i campi required presenti', () => {
    const strategy = createDefaultStrategy()

    // Verifica campi required root level
    expect(strategy.strategy_id).toBeDefined()
    expect(strategy.strategy_version).toBeDefined()
    expect(strategy.schema_version).toBeDefined()
    expect(strategy.name).toBeDefined()
    expect(strategy.author).toBeDefined()
    expect(strategy.license).toBeDefined()
    expect(strategy.description).toBeDefined()
    expect(strategy.tags).toBeDefined()
    expect(strategy.created_at).toBeDefined()
    expect(strategy.updated_at).toBeDefined()
    expect(strategy.enabled_default).toBeDefined()
    expect(strategy.changelog).toBeDefined()

    // Verifica nested objects required
    expect(strategy.instrument).toBeDefined()
    expect(strategy.entry_filters).toBeDefined()
    expect(strategy.campaign_rules).toBeDefined()
    expect(strategy.structure).toBeDefined()
    expect(strategy.selection_filters).toBeDefined()
    expect(strategy.exit_rules).toBeDefined()
    expect(strategy.execution_rules).toBeDefined()
    expect(strategy.monitoring).toBeDefined()
    expect(strategy.notifications).toBeDefined()
  })

  it('TEST-SW-01c-07: createDefaultStrategy("My Strat") → name === "My Strat"', () => {
    const strategy = createDefaultStrategy('My Strat')
    expect(strategy.name).toBe('My Strat')
  })

  // ============================================================================
  // createDefaultLeg Tests
  // ============================================================================

  it('TEST-SW-01c-08: createDefaultLeg(0, "sell") → target_delta === 0.30', () => {
    const leg = createDefaultLeg(0, 'sell')
    expect(leg.target_delta).toBe(0.3)
  })

  it('TEST-SW-01c-09: createDefaultLeg(1, "buy") → target_delta === 0.16', () => {
    const leg = createDefaultLeg(1, 'buy')
    expect(leg.target_delta).toBe(0.16)
  })

  it('TEST-SW-01c-10: createDefaultLeg(0, "sell", "call") → right === "call"', () => {
    const leg = createDefaultLeg(0, 'sell', 'call')
    expect(leg.right).toBe('call')
  })

  // ============================================================================
  // createDefaultHardStop Tests
  // ============================================================================

  it('TEST-SW-01c-11: createDefaultHardStop(0, "pnl_threshold") → threshold < 0', () => {
    const stop = createDefaultHardStop(0, 'pnl_threshold')
    expect(stop.threshold).toBeLessThan(0)
  })

  it('TEST-SW-01c-12: createDefaultHardStop(1, "portfolio_greek") → greek === "delta"', () => {
    const stop = createDefaultHardStop(1, 'portfolio_greek')
    expect(stop.greek).toBe('delta')
  })

  // ============================================================================
  // cloneStrategy Tests
  // ============================================================================

  it('TEST-SW-01c-13: cloneStrategy(orig, "New Name") → nuovo strategy_id generato', () => {
    const original = createDefaultStrategy('Original Strategy')
    const cloned = cloneStrategy(original, 'New Name')

    expect(cloned.strategy_id).toBeDefined()
    expect(cloned.strategy_id).not.toBe(original.strategy_id)
    expect(cloned.strategy_id).toBe('new-name')
  })

  it('TEST-SW-01c-14: cloneStrategy(orig, "New Name") → version === "1.0.0"', () => {
    const original = createDefaultStrategy('Original Strategy')
    original.strategy_version = '2.5.3' // Original has different version
    const cloned = cloneStrategy(original, 'New Name')

    expect(cloned.strategy_version).toBe('1.0.0')
  })

  // ============================================================================
  // incrementVersion Tests
  // ============================================================================

  it('TEST-SW-01c-15: incrementVersion("1.2.3", "major") → "2.0.0"', () => {
    const result = incrementVersion('1.2.3', 'major')
    expect(result).toBe('2.0.0')
  })

  it('TEST-SW-01c-16: incrementVersion("1.2.3", "minor") → "1.3.0"', () => {
    const result = incrementVersion('1.2.3', 'minor')
    expect(result).toBe('1.3.0')
  })

  it('TEST-SW-01c-17: incrementVersion("1.2.3", "patch") → "1.2.4"', () => {
    const result = incrementVersion('1.2.3', 'patch')
    expect(result).toBe('1.2.4')
  })

  // ============================================================================
  // generateTimestamp Tests
  // ============================================================================

  it('TEST-SW-01c-18: generateTimestamp() → formato ISO 8601 valido', () => {
    const timestamp = generateTimestamp()

    // Verifica formato ISO 8601
    expect(timestamp).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$/)

    // Verifica che sia parseable come Date
    const date = new Date(timestamp)
    expect(date.toISOString()).toBe(timestamp)
  })

  // ============================================================================
  // createInitialChangelog Tests
  // ============================================================================

  it('TEST-SW-01c-19: createInitialChangelog("Test") → array con 1 entry', () => {
    const changelog = createInitialChangelog('Test')
    expect(Array.isArray(changelog)).toBe(true)
    expect(changelog).toHaveLength(1)
  })

  it('TEST-SW-01c-20: createInitialChangelog("Test") → version "1.0.0"', () => {
    const changelog = createInitialChangelog('Test')
    expect(changelog[0].version).toBe('1.0.0')
  })

  // ============================================================================
  // Integration Test with Validator
  // ============================================================================

  it('INTEGRATION: createDefaultStrategy() creates valid structure (legs can be added in wizard)', () => {
    const strategy = createDefaultStrategy('Test Strategy') as StrategyDraft

    // Validator expects at least one leg, but default strategy is a template
    // Legs will be added by the user in the wizard
    const result = validateAll(strategy)

    // Should have only the expected error about missing legs
    expect(result.errors).toHaveLength(1)
    expect(result.errors[0].field).toBe('structure.legs')
    expect(result.errors[0].message).toBe('Almeno un leg è obbligatorio')

    // All other validations should pass
    // If we add a leg, it should be fully valid
    strategy.structure!.legs = [createDefaultLeg(0, 'sell')]
    const resultWithLeg = validateAll(strategy)
    expect(resultWithLeg.valid).toBe(true)
    expect(resultWithLeg.errors).toHaveLength(0)
  })

  // ============================================================================
  // Edge Cases
  // ============================================================================

  it('EDGE: generateStrategyId handles unicode characters', () => {
    const result = generateStrategyId('Test Strategía Piñata 中文')
    // Unicode special chars should be removed, leaving only ascii
    expect(result).toBe('test-stratega-piata')
  })

  it('EDGE: generateStrategyId handles empty string', () => {
    const result = generateStrategyId('')
    expect(result).toBe('')
  })

  it('EDGE: generateStrategyId handles only special characters', () => {
    const result = generateStrategyId('!@#$%^&*()')
    expect(result).toBe('')
  })

  it('EDGE: createDefaultLeg generates unique leg_id', () => {
    const leg0 = createDefaultLeg(0, 'sell')
    const leg1 = createDefaultLeg(1, 'buy')
    const leg2 = createDefaultLeg(2, 'sell')

    expect(leg0.leg_id).toBe('leg-1')
    expect(leg1.leg_id).toBe('leg-2')
    expect(leg2.leg_id).toBe('leg-3')
  })

  it('EDGE: createDefaultHardStop generates unique condition_id', () => {
    const stop0 = createDefaultHardStop(0, 'pnl_threshold')
    const stop1 = createDefaultHardStop(1, 'portfolio_greek')
    const stop2 = createDefaultHardStop(2, 'underlying_vs_leg_strike')

    expect(stop0.condition_id).toBe('stop-1')
    expect(stop1.condition_id).toBe('stop-2')
    expect(stop2.condition_id).toBe('stop-3')
  })

  it('EDGE: cloneStrategy preserves all original properties except identity fields', () => {
    const original = createDefaultStrategy('Original')
    // Add some custom properties
    original.description = 'Custom description'
    original.tags = ['tag1', 'tag2']
    original.structure!.legs = [createDefaultLeg(0, 'sell')]

    const cloned = cloneStrategy(original, 'Cloned')

    // Identity fields should be different
    expect(cloned.strategy_id).not.toBe(original.strategy_id)
    expect(cloned.name).not.toBe(original.name)
    expect(cloned.strategy_version).toBe('1.0.0')

    // Other properties should be preserved
    expect(cloned.description).toBe(original.description)
    expect(cloned.tags).toEqual(original.tags)
    expect(cloned.structure!.legs).toEqual(original.structure!.legs)
  })

  it('EDGE: incrementVersion handles invalid level gracefully', () => {
    const result = incrementVersion('1.2.3', 'invalid' as any)
    expect(result).toBe('1.2.3') // Returns unchanged
  })
})
