/**
 * Wizard Store Tests
 *
 * Test suite for wizard store state management, navigation, validation, and publish flow.
 * Implements all TEST-SW-02-XX tests from task specification.
 */

import { describe, it, expect, beforeEach, vi } from 'vitest'
import { useWizardStore } from '../src/stores/wizardStore'
import { createDefaultStrategy } from '../src/utils/sdf-defaults'
import type { StrategyDraft } from '../src/types/sdf-v1'

// ============================================================================
// SETUP & TEARDOWN
// ============================================================================

beforeEach(() => {
  // Reset store to initial state before each test
  useWizardStore.getState().resetWizard()
  vi.clearAllMocks()
})

// ============================================================================
// TEST-SW-02-01: setField with nested path
// ============================================================================

describe('TEST-SW-02-01: setField with nested path', () => {
  it('should update draft at deeply nested path', () => {
    const { setField, draft } = useWizardStore.getState()

    // Initial value
    expect(draft.entry_filters?.ivts?.suspend_threshold).toBe(1.15)

    // Update nested field
    setField('entry_filters.ivts.suspend_threshold', 1.2)

    // Verify update
    const updatedDraft = useWizardStore.getState().draft
    expect(updatedDraft.entry_filters?.ivts?.suspend_threshold).toBe(1.2)
  })

  it('should mark draft as dirty after field update', () => {
    const { setField } = useWizardStore.getState()

    expect(useWizardStore.getState().isDirty).toBe(false)

    setField('entry_filters.ivts.suspend_threshold', 1.2)

    expect(useWizardStore.getState().isDirty).toBe(true)
  })

  it('should update stepErrors after field change', () => {
    const { setField } = useWizardStore.getState()

    setField('entry_filters.ivts.suspend_threshold', 1.2)

    const { stepErrors, currentStep } = useWizardStore.getState()
    expect(stepErrors[currentStep]).toBeDefined()
  })
})

// ============================================================================
// TEST-SW-02-02: nextStep() with validation errors
// ============================================================================

describe('TEST-SW-02-02: nextStep() with validation errors', () => {
  it('should return false and not advance when step has validation errors', () => {
    const { setField, nextStep } = useWizardStore.getState()

    // Create validation error by setting invalid data
    // According to validator: name must be at least 3 chars
    setField('name', 'ab')

    const initialStep = useWizardStore.getState().currentStep

    // Try to advance
    const result = nextStep()

    // Should not advance
    expect(result).toBe(false)
    expect(useWizardStore.getState().currentStep).toBe(initialStep)
  })

  it('should populate stepErrors when validation fails', () => {
    const { setField, nextStep } = useWizardStore.getState()

    setField('name', 'ab')

    nextStep()

    const { stepErrors, currentStep } = useWizardStore.getState()
    expect(stepErrors[currentStep]).toBeDefined()
    expect(stepErrors[currentStep]!.length).toBeGreaterThan(0)
    expect(stepErrors[currentStep]!.some((e) => e.severity === 'error')).toBe(true)
  })
})

// ============================================================================
// TEST-SW-02-03: nextStep() with valid step
// ============================================================================

describe('TEST-SW-02-03: nextStep() with valid step', () => {
  it('should return true and advance to next step when step is valid', () => {
    const { nextStep } = useWizardStore.getState()

    const initialStep = useWizardStore.getState().currentStep

    // Step 1 should be valid with default data
    const result = nextStep()

    // Should advance
    expect(result).toBe(true)
    expect(useWizardStore.getState().currentStep).toBe(initialStep + 1)
  })

  it('should add new step to visitedSteps', () => {
    const { nextStep } = useWizardStore.getState()

    const initialVisited = [...useWizardStore.getState().visitedSteps]

    nextStep()

    const newVisited = useWizardStore.getState().visitedSteps
    expect(newVisited.length).toBe(initialVisited.length + 1)
    expect(newVisited).toContain(2)
  })
})

// ============================================================================
// TEST-SW-02-04: goToStep() with unvisited step
// ============================================================================

describe('TEST-SW-02-04: goToStep() with unvisited step', () => {
  it('should not navigate to unvisited step that is not next', () => {
    const { goToStep } = useWizardStore.getState()

    const initialStep = useWizardStore.getState().currentStep

    // Try to jump to step 5 without visiting 2, 3, 4
    goToStep(5)

    // Should not navigate
    expect(useWizardStore.getState().currentStep).toBe(initialStep)
  })
})

// ============================================================================
// TEST-SW-02-05: goToStep() to next step
// ============================================================================

describe('TEST-SW-02-05: goToStep() to next step', () => {
  it('should navigate to immediate next step', () => {
    const { goToStep } = useWizardStore.getState()

    // Should allow navigation to step 2 (immediate next)
    goToStep(2)

    expect(useWizardStore.getState().currentStep).toBe(2)
  })

  it('should add next step to visitedSteps', () => {
    const { goToStep } = useWizardStore.getState()

    goToStep(2)

    const { visitedSteps } = useWizardStore.getState()
    expect(visitedSteps).toContain(1)
    expect(visitedSteps).toContain(2)
  })
})

// ============================================================================
// TEST-SW-02-06: initFromJson() with valid JSON
// ============================================================================

describe('TEST-SW-02-06: initFromJson() with valid JSON', () => {
  it('should parse JSON and set mode to import', () => {
    const { initFromJson } = useWizardStore.getState()

    const validStrategy: StrategyDraft = {
      ...createDefaultStrategy(),
      strategy_id: 'test-strategy',
      name: 'Test Strategy',
    }

    const result = initFromJson(JSON.stringify(validStrategy))

    expect(result.ok).toBe(true)
    expect(result.errors).toHaveLength(0)

    const { mode, draft } = useWizardStore.getState()
    expect(mode).toBe('import')
    expect(draft.strategy_id).toBe('test-strategy')
    expect(draft.name).toBe('Test Strategy')
  })

  it('should mark all steps as visited when importing', () => {
    const { initFromJson, totalSteps } = useWizardStore.getState()

    const validStrategy: StrategyDraft = {
      ...createDefaultStrategy(),
      strategy_id: 'test-strategy',
    }

    initFromJson(JSON.stringify(validStrategy))

    const { visitedSteps } = useWizardStore.getState()
    expect(visitedSteps.length).toBe(totalSteps)
    expect(visitedSteps).toContain(1)
    expect(visitedSteps).toContain(totalSteps)
  })

  it('should set isDirty to false after import', () => {
    const { initFromJson } = useWizardStore.getState()

    const validStrategy: StrategyDraft = {
      ...createDefaultStrategy(),
      strategy_id: 'test-strategy',
    }

    initFromJson(JSON.stringify(validStrategy))

    expect(useWizardStore.getState().isDirty).toBe(false)
  })
})

// ============================================================================
// TEST-SW-02-07: initFromJson() with invalid JSON
// ============================================================================

describe('TEST-SW-02-07: initFromJson() with invalid JSON', () => {
  it('should return error for malformed JSON', () => {
    const { initFromJson } = useWizardStore.getState()

    const result = initFromJson('{ invalid }')

    expect(result.ok).toBe(false)
    expect(result.errors.length).toBeGreaterThan(0)
    expect(result.errors[0]).toContain('JSON non valido')
  })

  it('should return error for JSON without strategy_id', () => {
    const { initFromJson } = useWizardStore.getState()

    const invalidStrategy = {
      name: 'Test Strategy',
      // missing strategy_id
    }

    const result = initFromJson(JSON.stringify(invalidStrategy))

    expect(result.ok).toBe(false)
    expect(result.errors.length).toBeGreaterThan(0)
    expect(result.errors[0]).toContain('strategy_id')
  })
})

// ============================================================================
// TEST-SW-02-08: validateAllSteps() with complete draft
// ============================================================================

describe('TEST-SW-02-08: validateAllSteps() with complete draft', () => {
  it('should return true for complete valid draft', () => {
    const { validateAllSteps, setField } = useWizardStore.getState()

    // Add at least one leg (required by validator)
    setField('structure.legs', [
      {
        leg_id: 'leg-1',
        action: 'sell',
        right: 'put',
        target_dte: 45,
        dte_tolerance: 3,
        target_delta: 0.3,
        delta_tolerance: 0.05,
        quantity: 1,
        settlement_preference: 'PM',
        exclude_expiry_within_days: 0,
        role: 'income',
        order_group: 'combo',
      },
    ])

    const isValid = validateAllSteps()

    expect(isValid).toBe(true)
  })

  it('should set globalErrors to empty array when valid', () => {
    const { validateAllSteps, setField } = useWizardStore.getState()

    // Add at least one leg
    setField('structure.legs', [
      {
        leg_id: 'leg-1',
        action: 'sell',
        right: 'put',
        target_dte: 45,
        dte_tolerance: 3,
        target_delta: 0.3,
        delta_tolerance: 0.05,
        quantity: 1,
        settlement_preference: 'PM',
        exclude_expiry_within_days: 0,
        role: 'income',
        order_group: 'combo',
      },
    ])

    validateAllSteps()

    const { globalErrors } = useWizardStore.getState()
    expect(globalErrors).toHaveLength(0)
  })
})

// ============================================================================
// TEST-SW-02-09: publish() with validation failed
// ============================================================================

describe('TEST-SW-02-09: publish() with validation failed', () => {
  it('should set publishStatus to error when validation fails', async () => {
    const { publish, setField } = useWizardStore.getState()

    // Create invalid state (no legs)
    setField('structure.legs', [])

    await publish()

    const { publishStatus } = useWizardStore.getState()
    expect(publishStatus).toBe('error')
  })

  it('should set publishError message when validation fails', async () => {
    const { publish, setField } = useWizardStore.getState()

    setField('structure.legs', [])

    await publish()

    const { publishError } = useWizardStore.getState()
    expect(publishError).toBeTruthy()
    expect(publishError).toContain('Validazione fallita')
  })
})

// ============================================================================
// TEST-SW-02-10: resetWizard()
// ============================================================================

describe('TEST-SW-02-10: resetWizard()', () => {
  it('should reset currentStep to 1', () => {
    const { nextStep, resetWizard } = useWizardStore.getState()

    // Advance to step 2
    nextStep()
    expect(useWizardStore.getState().currentStep).toBe(2)

    // Reset
    resetWizard()

    expect(useWizardStore.getState().currentStep).toBe(1)
  })

  it('should reset mode to new', () => {
    const { initFromJson, resetWizard } = useWizardStore.getState()

    // Import a strategy
    const validStrategy: StrategyDraft = {
      ...createDefaultStrategy(),
      strategy_id: 'test-strategy',
    }
    initFromJson(JSON.stringify(validStrategy))

    expect(useWizardStore.getState().mode).toBe('import')

    // Reset
    resetWizard()

    expect(useWizardStore.getState().mode).toBe('new')
  })

  it('should reset draft to default strategy', () => {
    const { setField, resetWizard } = useWizardStore.getState()

    // Modify draft
    setField('name', 'Modified Strategy')

    // Reset
    resetWizard()

    const { draft } = useWizardStore.getState()
    expect(draft.name).toBe('Nuova Strategia')
  })

  it('should clear all errors and state flags', () => {
    const { setField, resetWizard } = useWizardStore.getState()

    // Create some state
    setField('name', 'Test')

    // Reset
    resetWizard()

    const { stepErrors, globalErrors, isDirty } = useWizardStore.getState()
    expect(Object.keys(stepErrors).length).toBe(0)
    expect(globalErrors).toHaveLength(0)
    expect(isDirty).toBe(false)
  })
})

// ============================================================================
// ADDITIONAL EDGE CASES
// ============================================================================

describe('Additional edge cases', () => {
  it('should not navigate below step 1', () => {
    const { goToStep } = useWizardStore.getState()

    goToStep(0)

    expect(useWizardStore.getState().currentStep).toBe(1)
  })

  it('should not navigate above totalSteps', () => {
    const { goToStep, totalSteps } = useWizardStore.getState()

    goToStep(totalSteps + 1)

    expect(useWizardStore.getState().currentStep).toBe(1)
  })

  it('should allow navigation to previously visited steps', () => {
    const { nextStep, goToStep, setField } = useWizardStore.getState()

    // Add a leg so step 2 validation passes
    setField('structure.legs', [
      {
        leg_id: 'leg-1',
        action: 'sell',
        right: 'put',
        target_dte: 45,
        dte_tolerance: 3,
        target_delta: 0.3,
        delta_tolerance: 0.05,
        quantity: 1,
        settlement_preference: 'PM',
        exclude_expiry_within_days: 0,
        role: 'income',
        order_group: 'combo',
      },
    ])

    // Navigate to step 2 and 3
    nextStep()
    nextStep()

    expect(useWizardStore.getState().currentStep).toBe(3)

    // Navigate back to step 2 (visited)
    goToStep(2)

    expect(useWizardStore.getState().currentStep).toBe(2)
  })

  it('prevStep should not go below step 1', () => {
    const { prevStep } = useWizardStore.getState()

    expect(useWizardStore.getState().currentStep).toBe(1)

    prevStep()

    expect(useWizardStore.getState().currentStep).toBe(1)
  })

  it('prevStep should navigate back one step', () => {
    const { nextStep, prevStep } = useWizardStore.getState()

    nextStep()
    expect(useWizardStore.getState().currentStep).toBe(2)

    prevStep()
    expect(useWizardStore.getState().currentStep).toBe(1)
  })
})
