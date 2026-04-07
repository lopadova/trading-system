/**
 * HardStopBuilder Component
 *
 * Manages an array of hard stop conditions:
 * - Add new hard stop condition
 * - Edit existing conditions
 * - Remove conditions
 * - Validates reference_leg_id against available legs
 */

import { useState } from 'react'
import type { HardStopCondition, StrategyLeg } from '../../../types/sdf-v1'
import { HardStopCard } from './HardStopCard'
import { Button } from '../../ui/Button'
import { PlusIcon } from '@heroicons/react/24/outline'

// ============================================================================
// TYPES
// ============================================================================

export interface HardStopBuilderProps {
  conditions: HardStopCondition[]
  availableLegs: StrategyLeg[]
  onChange: (conditions: HardStopCondition[]) => void
}

// ============================================================================
// HELPERS
// ============================================================================

/**
 * Generate a unique condition ID
 */
function generateConditionId(existingIds: string[]): string {
  let counter = 1
  while (existingIds.includes(`hs_${counter.toString().padStart(3, '0')}`)) {
    counter++
  }
  return `hs_${counter.toString().padStart(3, '0')}`
}

/**
 * Create a default hard stop condition
 */
function createDefaultCondition(conditionId: string): HardStopCondition {
  return {
    condition_id: conditionId,
    type: 'pnl_threshold',
    operator: 'lt',
    threshold: -1000,
    severity: 'high',
    close_sequence: 'all_legs',
  }
}

/**
 * Validate hard stop condition
 */
function validateCondition(
  condition: HardStopCondition,
  availableLegs: StrategyLeg[]
): { valid: boolean; error?: string } {
  // Check reference_leg_id if type requires it
  if (condition.type === 'underlying_vs_leg_strike') {
    if (!condition.reference_leg_id) {
      return { valid: false, error: 'Reference leg is required for this type' }
    }
    const legExists = availableLegs.some((leg) => leg.leg_id === condition.reference_leg_id)
    if (!legExists) {
      return {
        valid: false,
        error: `Reference leg "${condition.reference_leg_id}" not found in strategy structure`,
      }
    }
  }

  // Check greek if type requires it
  if (condition.type === 'portfolio_greek') {
    if (!condition.greek) {
      return { valid: false, error: 'Greek is required for this type' }
    }
  }

  return { valid: true }
}

// ============================================================================
// COMPONENT
// ============================================================================

export function HardStopBuilder({
  conditions,
  availableLegs,
  onChange,
}: HardStopBuilderProps) {
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({})

  const handleAddCondition = () => {
    const existingIds = conditions.map((c) => c.condition_id)
    const newId = generateConditionId(existingIds)
    const newCondition = createDefaultCondition(newId)
    onChange([...conditions, newCondition])
  }

  const handleUpdateCondition = (index: number, updated: HardStopCondition) => {
    const newConditions = [...conditions]
    newConditions[index] = updated
    onChange(newConditions)

    // Validate the updated condition
    const validation = validateCondition(updated, availableLegs)
    setValidationErrors((prev) => {
      const next = { ...prev }
      if (validation.valid) {
        delete next[updated.condition_id]
      } else {
        next[updated.condition_id] = validation.error || 'Invalid condition'
      }
      return next
    })
  }

  const handleRemoveCondition = (index: number) => {
    const condition = conditions[index]
    if (!condition) return

    const conditionId = condition.condition_id
    const newConditions = conditions.filter((_, i) => i !== index)
    onChange(newConditions)

    // Remove validation error if exists
    setValidationErrors((prev) => {
      const next = { ...prev }
      delete next[conditionId]
      return next
    })
  }

  return (
    <div className="space-y-4">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-lg font-semibold text-[var(--wz-text)]">Hard Stop Conditions</h3>
          <p className="text-sm text-[var(--wz-muted)]">
            Condizioni di uscita automatica per gestione del rischio
          </p>
        </div>
        <Button
          onClick={handleAddCondition}
          variant="secondary"
          className="border-[var(--wz-error)] text-[var(--wz-error)] hover:bg-[var(--wz-error)] hover:text-white"
        >
          <PlusIcon className="w-4 h-4 mr-2" />
          Aggiungi Hard Stop
        </Button>
      </div>

      {/* Conditions List */}
      {conditions.length === 0 ? (
        <div className="p-8 text-center rounded-lg border-2 border-dashed border-[var(--wz-border)] bg-[var(--wz-surface)]">
          <p className="text-[var(--wz-muted)]">
            Nessun hard stop configurato. Clicca "Aggiungi Hard Stop" per iniziare.
          </p>
        </div>
      ) : (
        <div className="space-y-4">
          {conditions.map((condition, index) => (
            <HardStopCard
              key={condition.condition_id}
              condition={condition}
              availableLegs={availableLegs}
              onChange={(updated) => handleUpdateCondition(index, updated)}
              onRemove={() => handleRemoveCondition(index)}
              hasError={!!validationErrors[condition.condition_id]}
              errorMessage={validationErrors[condition.condition_id] ?? undefined}
            />
          ))}
        </div>
      )}

      {/* Summary */}
      {conditions.length > 0 && (
        <div className="p-3 rounded-lg bg-[var(--wz-error)]/10 border border-[var(--wz-error)]/20">
          <p className="text-sm text-[var(--wz-text)]">
            <strong>Totale Hard Stops:</strong> {conditions.length} condition
            {conditions.length !== 1 ? 's' : ''}
            {Object.keys(validationErrors).length > 0 && (
              <span className="ml-2 text-[var(--wz-error)]">
                ({Object.keys(validationErrors).length} con errori)
              </span>
            )}
          </p>
        </div>
      )}
    </div>
  )
}
