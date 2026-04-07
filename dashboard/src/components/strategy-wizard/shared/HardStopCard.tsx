/**
 * HardStopCard Component
 *
 * Displays a single hard stop condition with:
 * - Condition ID (editable)
 * - Type selector (underlying_vs_leg_strike, portfolio_greek, pnl_threshold)
 * - Conditional fields based on type
 * - Operator, threshold, severity, close_sequence
 * - Edit/Remove actions
 */

import type { HardStopCondition, StrategyLeg } from '../../../types/sdf-v1'
import { XMarkIcon, ExclamationTriangleIcon } from '@heroicons/react/24/outline'

// ============================================================================
// TYPES
// ============================================================================

export interface HardStopCardProps {
  condition: HardStopCondition
  availableLegs: StrategyLeg[]
  onChange: (updated: HardStopCondition) => void
  onRemove: () => void
  hasError?: boolean
  errorMessage?: string | undefined
}

// ============================================================================
// CONSTANTS
// ============================================================================

const HARD_STOP_TYPES = [
  { value: 'underlying_vs_leg_strike', label: 'Underlying vs Leg Strike' },
  { value: 'portfolio_greek', label: 'Portfolio Greek' },
  { value: 'pnl_threshold', label: 'P&L Threshold' },
] as const

const GREEKS = [
  { value: 'delta', label: 'Delta' },
  { value: 'theta', label: 'Theta' },
  { value: 'gamma', label: 'Gamma' },
  { value: 'vega', label: 'Vega' },
] as const

const OPERATORS = [
  { value: 'lt', label: '<' },
  { value: 'gt', label: '>' },
  { value: 'lte', label: '≤' },
  { value: 'gte', label: '≥' },
] as const

const SEVERITIES = [
  { value: 'critical', label: 'Critical', color: 'var(--wz-error)' },
  { value: 'high', label: 'High', color: 'var(--wz-warning)' },
  { value: 'medium', label: 'Medium', color: 'var(--wz-muted)' },
] as const

const CLOSE_SEQUENCES = [
  { value: 'all_legs', label: 'Close All Legs' },
  { value: 'combo_only', label: 'Combo Only' },
  { value: 'hedge_only', label: 'Hedge Only' },
] as const

// ============================================================================
// COMPONENT
// ============================================================================

export function HardStopCard({
  condition,
  availableLegs,
  onChange,
  onRemove,
  hasError = false,
  errorMessage,
}: HardStopCardProps) {
  const updateField = <K extends keyof HardStopCondition>(
    field: K,
    value: HardStopCondition[K]
  ) => {
    onChange({ ...condition, [field]: value })
  }

  const showReferenceLeg = condition.type === 'underlying_vs_leg_strike'
  const showGreek = condition.type === 'portfolio_greek'

  return (
    <div
      className={`
        p-4 rounded-lg border-2 transition-all
        ${
          hasError
            ? 'border-[var(--wz-error)] bg-[var(--wz-error)]/5'
            : 'border-[var(--wz-border)] bg-[var(--wz-surface)]'
        }
      `}
    >
      {/* Header: Condition ID + Remove Button */}
      <div className="flex items-center gap-3 mb-4">
        <div className="flex-1">
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Condition ID</label>
          <input
            type="text"
            value={condition.condition_id}
            onChange={(e) => updateField('condition_id', e.target.value)}
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-elevated)] border border-[var(--wz-border)]
              text-[var(--wz-text)] font-mono text-sm
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
            placeholder="e.g., hs_001"
          />
        </div>

        <button
          type="button"
          onClick={onRemove}
          className="
            mt-5 p-2 rounded-lg
            border border-[var(--wz-error)] text-[var(--wz-error)]
            hover:bg-[var(--wz-error)] hover:text-white
            transition-colors
          "
          aria-label="Remove hard stop"
        >
          <XMarkIcon className="w-5 h-5" />
        </button>
      </div>

      {/* Error Message */}
      {hasError && errorMessage && (
        <div className="mb-4 flex items-start gap-2 text-sm text-[var(--wz-error)] bg-[var(--wz-error)]/10 p-3 rounded-lg">
          <ExclamationTriangleIcon className="w-4 h-4 flex-shrink-0 mt-0.5" />
          <span>{errorMessage}</span>
        </div>
      )}

      {/* Type Selector */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        <div>
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Type</label>
          <select
            value={condition.type}
            onChange={(e) =>
              updateField('type', e.target.value as HardStopCondition['type'])
            }
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-elevated)] border border-[var(--wz-border)]
              text-[var(--wz-text)]
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
          >
            {HARD_STOP_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
        </div>

        {/* Reference Leg (conditional) */}
        {showReferenceLeg && (
          <div>
            <label className="text-xs text-[var(--wz-muted)] block mb-1" htmlFor={`ref-leg-${condition.condition_id}`}>
              Reference Leg <span className="text-[var(--wz-error)]">*</span>
            </label>
            <select
              id={`ref-leg-${condition.condition_id}`}
              aria-label="Reference Leg"
              value={condition.reference_leg_id ?? ''}
              onChange={(e) => updateField('reference_leg_id', e.target.value || undefined)}
              className="
                w-full px-3 py-2 rounded-lg
                bg-[var(--wz-elevated)] border border-[var(--wz-border)]
                text-[var(--wz-text)]
                focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                transition-colors
              "
            >
              <option value="">-- Select Leg --</option>
              {availableLegs.map((leg) => (
                <option key={leg.leg_id} value={leg.leg_id}>
                  {leg.leg_id} ({leg.role || `${leg.action} ${leg.right}`})
                </option>
              ))}
            </select>
          </div>
        )}

        {/* Greek (conditional) */}
        {showGreek && (
          <div>
            <label className="text-xs text-[var(--wz-muted)] block mb-1" htmlFor={`greek-${condition.condition_id}`}>
              Greek <span className="text-[var(--wz-error)]">*</span>
            </label>
            <select
              id={`greek-${condition.condition_id}`}
              aria-label="Greek"
              value={condition.greek ?? ''}
              onChange={(e) =>
                updateField('greek', (e.target.value as HardStopCondition['greek']) || undefined)
              }
              className="
                w-full px-3 py-2 rounded-lg
                bg-[var(--wz-elevated)] border border-[var(--wz-border)]
                text-[var(--wz-text)]
                focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                transition-colors
              "
            >
              <option value="">-- Select Greek --</option>
              {GREEKS.map((g) => (
                <option key={g.value} value={g.value}>
                  {g.label}
                </option>
              ))}
            </select>
          </div>
        )}
      </div>

      {/* Operator + Threshold */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <div>
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Operator</label>
          <select
            value={condition.operator}
            onChange={(e) =>
              updateField('operator', e.target.value as HardStopCondition['operator'])
            }
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-elevated)] border border-[var(--wz-border)]
              text-[var(--wz-text)]
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
          >
            {OPERATORS.map((op) => (
              <option key={op.value} value={op.value}>
                {op.label}
              </option>
            ))}
          </select>
        </div>

        <div>
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Threshold</label>
          <input
            type="number"
            step="any"
            value={condition.threshold}
            onChange={(e) => updateField('threshold', parseFloat(e.target.value) || 0)}
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-elevated)] border border-[var(--wz-border)]
              text-[var(--wz-text)] font-mono
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
          />
        </div>
      </div>

      {/* Severity + Close Sequence */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mt-4">
        <div>
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Severity</label>
          <div className="flex gap-2">
            {SEVERITIES.map((sev) => (
              <label
                key={sev.value}
                className={`
                  flex-1 px-3 py-2 rounded-lg border-2 cursor-pointer text-center transition-all
                  ${
                    condition.severity === sev.value
                      ? 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)]'
                      : 'border-[var(--wz-border)] bg-[var(--wz-elevated)]'
                  }
                `}
              >
                <input
                  type="radio"
                  name={`severity_${condition.condition_id}`}
                  value={sev.value}
                  checked={condition.severity === sev.value}
                  onChange={(e) =>
                    updateField('severity', e.target.value as HardStopCondition['severity'])
                  }
                  className="sr-only"
                />
                <span className="text-sm font-semibold" style={{ color: sev.color }}>
                  {sev.label}
                </span>
              </label>
            ))}
          </div>
        </div>

        <div>
          <label className="text-xs text-[var(--wz-muted)] block mb-1">Close Sequence</label>
          <select
            value={condition.close_sequence}
            onChange={(e) =>
              updateField('close_sequence', e.target.value as HardStopCondition['close_sequence'])
            }
            className="
              w-full px-3 py-2 rounded-lg
              bg-[var(--wz-elevated)] border border-[var(--wz-border)]
              text-[var(--wz-text)]
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
          >
            {CLOSE_SEQUENCES.map((cs) => (
              <option key={cs.value} value={cs.value}>
                {cs.label}
              </option>
            ))}
          </select>
        </div>
      </div>
    </div>
  )
}
