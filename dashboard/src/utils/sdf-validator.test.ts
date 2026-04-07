/**
 * SDF v1 Validator Tests
 *
 * Test-first approach: All 14 tests defined in T-01b specification.
 * Tests cover field validation, cross-field validation, and wizard step validation.
 */

import { describe, it, expect } from 'vitest'
import {
  validateAll,
  validateField,
  validateCrossField,
  validateStep,
} from './sdf-validator'
import type { StrategyDraft } from '../types/sdf-v1'

describe('SDF Validator', () => {
  // TEST-SW-01b-01: validateAll(emptyDraft) → valid=false, errors.length > 5
  it('TEST-SW-01b-01: rejects empty draft with multiple errors', () => {
    const emptyDraft: StrategyDraft = {}
    const result = validateAll(emptyDraft)

    expect(result.valid).toBe(false)
    expect(result.errors.length).toBeGreaterThan(5)
  })

  // TEST-SW-01b-02: validateAll(completeDraft) → valid=true, errors.length === 0
  it('TEST-SW-01b-02: accepts complete valid draft', () => {
    const completeDraft: StrategyDraft = {
      name: 'Test Strategy',
      strategy_id: 'test-strategy',
      author: 'Test Author',
      description: 'A valid test strategy',
      structure: {
        legs: [
          {
            leg_id: 'leg-001',
            action: 'sell' as const,
            right: 'put' as const,
            target_dte: 45,
            dte_tolerance: 5,
            target_delta: 0.3,
            delta_tolerance: 0.05,
            quantity: 1,
            settlement_preference: 'PM' as const,
            exclude_expiry_within_days: 0,
            role: 'short put',
            order_group: 'combo' as const,
          },
        ],
      },
      entry_filters: {
        ivts: {
          enabled: true,
          formula: 'test',
          suspend_threshold: 1.2,
          resume_threshold: 1.1,
          staleness_max_minutes: 30,
          fallback_behavior: 'block' as const,
        },
      },
      exit_rules: {
        profit_target_usd: 100,
        stop_loss_usd: 200,
        max_days_in_position: 30,
        hard_stop_conditions: [],
      },
      campaign_rules: {
        max_active_campaigns: 3,
        max_per_rolling_week: 5,
        week_start_day: 'monday' as const,
        overlap_check_enabled: true,
      },
    }

    const result = validateAll(completeDraft)

    expect(result.valid).toBe(true)
    expect(result.errors.length).toBe(0)
  })

  // TEST-SW-01b-03: suspend=1.10, resume=1.10 → cross-field error su suspend_threshold
  it('TEST-SW-01b-03: detects IVTS thresholds equal (suspend <= resume)', () => {
    const draft: StrategyDraft = {
      entry_filters: {
        ivts: {
          suspend_threshold: 1.1,
          resume_threshold: 1.1,
        },
      },
    }

    const errors = validateCrossField(draft)

    expect(errors.length).toBeGreaterThan(0)
    const ivtsError = errors.find((e) => e.field === 'entry_filters.ivts.suspend_threshold')
    expect(ivtsError).toBeDefined()
    expect(ivtsError?.message).toContain('maggiore')
  })

  // TEST-SW-01b-04: suspend=1.15, resume=1.10 → nessun errore cross-field
  it('TEST-SW-01b-04: accepts valid IVTS thresholds (suspend > resume)', () => {
    const draft: StrategyDraft = {
      entry_filters: {
        ivts: {
          suspend_threshold: 1.15,
          resume_threshold: 1.1,
        },
      },
    }

    const errors = validateCrossField(draft)

    const ivtsError = errors.find((e) => e.field === 'entry_filters.ivts.suspend_threshold')
    expect(ivtsError).toBeUndefined()
  })

  // TEST-SW-01b-05: target_delta = 1.5 → error "deve essere tra 0.01 e 0.99"
  it('TEST-SW-01b-05: rejects delta out of range (> 0.99)', () => {
    const draft: StrategyDraft = {}
    const error = validateField('structure.legs[0].target_delta', 1.5, draft)

    expect(error).not.toBeNull()
    expect(error?.message).toContain('tra 0.01 e 0.99')
  })

  // TEST-SW-01b-06: target_delta = 0.30 → nessun errore
  it('TEST-SW-01b-06: accepts valid delta value', () => {
    const draft: StrategyDraft = {}
    const error = validateField('structure.legs[0].target_delta', 0.3, draft)

    expect(error).toBeNull()
  })

  // TEST-SW-01b-07: hard_stop con reference_leg_id="leg-999" non esistente → error descrittivo
  it('TEST-SW-01b-07: detects non-existent reference_leg_id in hard stop', () => {
    const draft: StrategyDraft = {
      structure: {
        legs: [
          {
            leg_id: 'leg-001',
            action: 'sell' as const,
            right: 'put' as const,
            target_dte: 45,
            dte_tolerance: 5,
            target_delta: 0.3,
            delta_tolerance: 0.05,
            quantity: 1,
            settlement_preference: 'PM' as const,
            exclude_expiry_within_days: 0,
            role: 'short put',
            order_group: 'combo' as const,
          },
        ],
      },
      exit_rules: {
        hard_stop_conditions: [
          {
            condition_id: 'stop-001',
            type: 'underlying_vs_leg_strike' as const,
            reference_leg_id: 'leg-999',
            operator: 'gt' as const,
            threshold: 1.1,
            severity: 'high' as const,
            close_sequence: 'all_legs' as const,
          },
        ],
      },
    }

    const errors = validateCrossField(draft)

    expect(errors.length).toBeGreaterThan(0)
    const hardStopError = errors.find((e) => e.field.includes('reference_leg_id'))
    expect(hardStopError).toBeDefined()
    expect(hardStopError?.message).toContain('leg-999')
    expect(hardStopError?.message).toContain('non trovato')
  })

  // TEST-SW-01b-08: hard_stop con reference_leg_id esistente → nessun errore
  it('TEST-SW-01b-08: accepts valid reference_leg_id in hard stop', () => {
    const draft: StrategyDraft = {
      structure: {
        legs: [
          {
            leg_id: 'leg-001',
            action: 'sell' as const,
            right: 'put' as const,
            target_dte: 45,
            dte_tolerance: 5,
            target_delta: 0.3,
            delta_tolerance: 0.05,
            quantity: 1,
            settlement_preference: 'PM' as const,
            exclude_expiry_within_days: 0,
            role: 'short put',
            order_group: 'combo' as const,
          },
        ],
      },
      exit_rules: {
        hard_stop_conditions: [
          {
            condition_id: 'stop-001',
            type: 'underlying_vs_leg_strike' as const,
            reference_leg_id: 'leg-001',
            operator: 'gt' as const,
            threshold: 1.1,
            severity: 'high' as const,
            close_sequence: 'all_legs' as const,
          },
        ],
      },
    }

    const errors = validateCrossField(draft)

    const hardStopError = errors.find((e) => e.field.includes('reference_leg_id'))
    expect(hardStopError).toBeUndefined()
  })

  // TEST-SW-01b-09: legs array vuoto → error "almeno un leg obbligatorio"
  it('TEST-SW-01b-09: requires at least one leg', () => {
    const draft: StrategyDraft = {
      structure: {
        legs: [],
      },
    }

    const errors = validateCrossField(draft)

    expect(errors.length).toBeGreaterThan(0)
    const legsError = errors.find((e) => e.field === 'structure.legs')
    expect(legsError).toBeDefined()
    expect(legsError?.message).toContain('Almeno un leg')
  })

  // TEST-SW-01b-10: legs con leg_id duplicati → error "leg ID duplicato"
  it('TEST-SW-01b-10: detects duplicate leg_id', () => {
    const draft: StrategyDraft = {
      structure: {
        legs: [
          {
            leg_id: 'leg-001',
            action: 'sell' as const,
            right: 'put' as const,
            target_dte: 45,
            dte_tolerance: 5,
            target_delta: 0.3,
            delta_tolerance: 0.05,
            quantity: 1,
            settlement_preference: 'PM' as const,
            exclude_expiry_within_days: 0,
            role: 'short put',
            order_group: 'combo' as const,
          },
          {
            leg_id: 'leg-001', // DUPLICATO
            action: 'buy' as const,
            right: 'call' as const,
            target_dte: 45,
            dte_tolerance: 5,
            target_delta: 0.16,
            delta_tolerance: 0.05,
            quantity: 1,
            settlement_preference: 'PM' as const,
            exclude_expiry_within_days: 0,
            role: 'long call hedge',
            order_group: 'standalone' as const,
          },
        ],
      },
    }

    const errors = validateCrossField(draft)

    expect(errors.length).toBeGreaterThan(0)
    const duplicateError = errors.find(
      (e) => e.field === 'structure.legs' && e.message.includes('duplicato')
    )
    expect(duplicateError).toBeDefined()
    expect(duplicateError?.message).toContain('leg-001')
  })

  // TEST-SW-01b-11: validateField('name', 'ab', draft) → error "almeno 3 caratteri"
  it('TEST-SW-01b-11: rejects short name (< 3 chars)', () => {
    const draft: StrategyDraft = {}
    const error = validateField('name', 'ab', draft)

    expect(error).not.toBeNull()
    expect(error?.message).toContain('almeno 3 caratteri')
  })

  // TEST-SW-01b-12: validateField('strategy_id', 'My Strategy', draft) → error "solo minuscole"
  it('TEST-SW-01b-12: rejects strategy_id with uppercase and spaces', () => {
    const draft: StrategyDraft = {}
    const error = validateField('strategy_id', 'My Strategy', draft)

    expect(error).not.toBeNull()
    expect(error?.message).toContain('lettere minuscole')
  })

  // TEST-SW-01b-13: validateStep(1, draftWithoutName) → errors contiene error su 'name'
  it('TEST-SW-01b-13: step validation detects missing required field', () => {
    const draftWithoutName: StrategyDraft = {
      strategy_id: 'test-strategy',
      author: 'Author',
      description: 'Description',
    }

    const errors = validateStep(1, draftWithoutName)

    expect(errors.length).toBeGreaterThan(0)
    const nameError = errors.find((e) => e.field === 'name')
    expect(nameError).toBeDefined()
  })

  // TEST-SW-01b-14: validateStep(1, draftWithName) → nessun errore
  it('TEST-SW-01b-14: step validation passes with complete step fields', () => {
    const draftWithName: StrategyDraft = {
      name: 'Complete Name',
      strategy_id: 'complete-strategy',
      author: 'Author Name',
      description: 'A complete description',
    }

    const errors = validateStep(1, draftWithName)

    expect(errors.length).toBe(0)
  })
})
