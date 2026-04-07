/**
 * Step07ExitRules Component
 *
 * Wizard Step 7: Exit Rules
 *
 * Configures profit targets, stop losses, and hard stop conditions:
 * - Profit target (USD)
 * - Stop loss (USD)
 * - Maximum days in position
 * - Hard stop conditions (array managed by HardStopBuilder)
 * - Preview of exit scenarios
 */

import { useWizardStore } from '../../../stores/wizardStore'
import { FieldWithTooltip } from '../shared/FieldWithTooltip'
import { HardStopBuilder } from '../shared/HardStopBuilder'
import { Card } from '../../ui/Card'
import type { HardStopCondition, StrategyLeg } from '../../../types/sdf-v1'
import {
  CheckCircleIcon,
  ExclamationCircleIcon,
  ClockIcon,
  ShieldExclamationIcon,
} from '@heroicons/react/24/outline'

// ============================================================================
// COMPONENT
// ============================================================================

export function Step07ExitRules() {
  const { draft, setField } = useWizardStore()

  // Extract current values with defaults
  const profitTargetUsd = draft.exit_rules?.profit_target_usd ?? 0
  const stopLossUsd = draft.exit_rules?.stop_loss_usd ?? 0
  const maxDaysInPosition = draft.exit_rules?.max_days_in_position ?? 0
  const hardStopConditions = (draft.exit_rules?.hard_stop_conditions ?? []).filter(
    (condition): condition is HardStopCondition =>
      condition != null && typeof condition.condition_id === 'string'
  )

  // Get available legs for hard stop builder (filter out undefined and incomplete)
  const allLegs = [
    ...(draft.structure?.legs?.filter((leg): leg is StrategyLeg =>
      leg != null && typeof leg.leg_id === 'string'
    ) ?? []),
    ...(draft.structure?.protection_legs?.filter((leg): leg is StrategyLeg =>
      leg != null && typeof leg.leg_id === 'string'
    ) ?? []),
  ]

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold mb-2">Regole di Uscita</h2>
        <p className="text-muted">
          Definisci i target di profitto, stop loss, e condizioni automatiche di chiusura
        </p>
      </div>

      {/* Target & Stop Loss Card */}
      <Card className="p-6 space-y-6">
        <h3 className="text-lg font-semibold">Target e Stop Loss</h3>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {/* Profit Target */}
          <FieldWithTooltip
            label="Profit Target (USD)"
            tooltip={{
              description:
                'Profitto target in USD al quale chiudere automaticamente la posizione. Imposta 0 per disabilitare.',
              example: '$2000 (conservative), $5000 (moderate), $10000 (aggressive)',
              warning:
                'Target troppo alti potrebbero non essere mai raggiunti. Target troppo bassi potrebbero limitare il potenziale di guadagno.',
            }}
          >
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--wz-muted)]">
                $
              </span>
              <input
                type="number"
                min={0}
                step={100}
                value={profitTargetUsd}
                onChange={(e) =>
                  setField('exit_rules.profit_target_usd', parseFloat(e.target.value) || 0)
                }
                className="
                  w-full pl-8 pr-4 py-2 rounded-lg
                  bg-[var(--wz-surface)] border border-[var(--wz-border)]
                  text-[var(--wz-text)] font-mono
                  focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                  transition-colors
                "
                placeholder="0 = disabled"
              />
            </div>
          </FieldWithTooltip>

          {/* Stop Loss */}
          <FieldWithTooltip
            label="Stop Loss (USD)"
            tooltip={{
              description:
                'Perdita massima in USD al quale chiudere automaticamente la posizione. Imposta 0 per disabilitare.',
              example: '$5000 (tight), $10000 (moderate), $20000 (wide)',
              warning:
                'Stop loss troppo stretto può portare a chiusure premature. Stop loss troppo ampio aumenta il rischio massimo.',
            }}
          >
            <div className="relative">
              <span className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--wz-muted)]">
                $
              </span>
              <input
                type="number"
                min={0}
                step={100}
                value={stopLossUsd}
                onChange={(e) =>
                  setField('exit_rules.stop_loss_usd', parseFloat(e.target.value) || 0)
                }
                className="
                  w-full pl-8 pr-4 py-2 rounded-lg
                  bg-[var(--wz-surface)] border border-[var(--wz-border)]
                  text-[var(--wz-text)] font-mono
                  focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                  transition-colors
                "
                placeholder="0 = disabled"
              />
            </div>
          </FieldWithTooltip>

          {/* Max Days in Position */}
          <FieldWithTooltip
            label="Max Giorni in Posizione"
            tooltip={{
              description:
                'Numero massimo di giorni per cui tenere aperta la posizione, indipendentemente dal P&L. Imposta 0 per illimitato.',
              example: '30 (monthly), 60 (2 months), 90 (quarterly)',
              warning:
                'Per strategie SPX credit spread, valori comuni sono 30-45 giorni. Valori troppo lunghi aumentano il rischio di eventi imprevisti.',
            }}
          >
            <input
              type="number"
              min={0}
              max={365}
              step={1}
              value={maxDaysInPosition}
              onChange={(e) =>
                setField('exit_rules.max_days_in_position', parseInt(e.target.value, 10) || 0)
              }
              className="
                w-full px-4 py-2 rounded-lg
                bg-[var(--wz-surface)] border border-[var(--wz-border)]
                text-[var(--wz-text)] font-mono
                focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                transition-colors
              "
              placeholder="0 = unlimited"
            />
            <div className="mt-2 text-sm text-[var(--wz-muted)]">Range: 0 – 365 giorni</div>
          </FieldWithTooltip>
        </div>
      </Card>

      {/* Hard Stop Conditions */}
      <Card className="p-6">
        <HardStopBuilder
          conditions={hardStopConditions}
          availableLegs={allLegs}
          onChange={(conditions) => setField('exit_rules.hard_stop_conditions', conditions)}
        />
      </Card>

      {/* Exit Scenarios Preview */}
      <Card className="p-6 bg-blue-500/10 border-blue-500/20">
        <h3 className="text-lg font-semibold mb-4">Scenari di Uscita</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Profit Target */}
          <div className="flex items-start gap-3">
            <CheckCircleIcon className="w-6 h-6 text-[var(--wz-success)] flex-shrink-0 mt-0.5" />
            <div>
              <div className="font-semibold text-[var(--wz-success)]">Target</div>
              <div className="text-sm text-[var(--wz-muted)]">
                {profitTargetUsd > 0 ? `+$${profitTargetUsd}` : 'Disabled'}
              </div>
            </div>
          </div>

          {/* Stop Loss */}
          <div className="flex items-start gap-3">
            <ExclamationCircleIcon className="w-6 h-6 text-[var(--wz-error)] flex-shrink-0 mt-0.5" />
            <div>
              <div className="font-semibold text-[var(--wz-error)]">Stop Loss</div>
              <div className="text-sm text-[var(--wz-muted)]">
                {stopLossUsd > 0 ? `-$${stopLossUsd}` : 'Disabled'}
              </div>
            </div>
          </div>

          {/* Max Days */}
          <div className="flex items-start gap-3">
            <ClockIcon className="w-6 h-6 text-[var(--wz-warning)] flex-shrink-0 mt-0.5" />
            <div>
              <div className="font-semibold text-[var(--wz-warning)]">Max Giorni</div>
              <div className="text-sm text-[var(--wz-muted)]">
                {maxDaysInPosition > 0 ? `${maxDaysInPosition} gg` : 'Unlimited'}
              </div>
            </div>
          </div>

          {/* Hard Stops */}
          <div className="flex items-start gap-3">
            <ShieldExclamationIcon className="w-6 h-6 text-[var(--wz-error)] flex-shrink-0 mt-0.5" />
            <div>
              <div className="font-semibold text-[var(--wz-error)]">Hard Stops</div>
              <div className="text-sm text-[var(--wz-muted)]">
                {hardStopConditions.length} condition
                {hardStopConditions.length !== 1 ? 's' : ''}
              </div>
            </div>
          </div>
        </div>
      </Card>
    </div>
  )
}
