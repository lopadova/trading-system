/**
 * Step09Monitoring Component
 *
 * Wizard Step 9: Monitoring & Notifications
 *
 * Configures monitoring intervals and notification preferences:
 * - Greeks snapshot interval
 * - Risk check interval
 * - Notification toggles for various events
 *   (on_hard_stop_triggered is non-disableable)
 */

import { useState } from 'react'
import { useWizardStore } from '../../../stores/wizardStore'
import { FieldWithTooltip } from '../shared/FieldWithTooltip'
import { Card } from '../../ui/Card'
import { ExclamationTriangleIcon } from '@heroicons/react/24/outline'

// ============================================================================
// CONSTANTS
// ============================================================================

const NOTIFICATION_TOGGLES = [
  {
    key: 'on_campaign_opened' as const,
    label: 'Campaign Opened',
    description: 'Notifica quando viene aperta una nuova campagna',
    disableable: true,
  },
  {
    key: 'on_target_hit' as const,
    label: 'Target Hit',
    description: 'Notifica quando viene raggiunto il profit target',
    disableable: true,
  },
  {
    key: 'on_stop_loss_hit' as const,
    label: 'Stop Loss Hit',
    description: 'Notifica quando viene raggiunto lo stop loss',
    disableable: true,
  },
  {
    key: 'on_hard_stop_triggered' as const,
    label: 'Hard Stop Triggered',
    description: 'Notifica quando viene triggerato un hard stop (NON DISABILITABILE)',
    disableable: false,
  },
  {
    key: 'on_max_days_close' as const,
    label: 'Max Days Close',
    description: 'Notifica quando la posizione viene chiusa per max giorni',
    disableable: true,
  },
  {
    key: 'on_ivts_state_change' as const,
    label: 'IVTS State Change',
    description: 'Notifica quando cambia lo stato IVTS (suspend/resume)',
    disableable: true,
  },
] as const

// ============================================================================
// COMPONENT
// ============================================================================

export function Step09Monitoring() {
  const { draft, setField } = useWizardStore()

  // State for hard_stop warning
  const [showHardStopWarning, setShowHardStopWarning] = useState(false)

  // Extract current values with defaults
  const greeksInterval = draft.monitoring?.greeks_snapshot_interval_minutes ?? 15
  const riskCheckInterval = draft.monitoring?.risk_check_interval_minutes ?? 5
  const notifications = draft.notifications ?? {}

  const handleNotificationToggle = (key: keyof typeof notifications, value: boolean) => {
    // Special case: prevent disabling hard_stop_triggered
    if (key === 'on_hard_stop_triggered' && !value) {
      setShowHardStopWarning(true)
      // Force it back to true
      setTimeout(() => {
        setField(`notifications.${key}`, true)
        setShowHardStopWarning(false)
      }, 2000)
      return
    }

    setField(`notifications.${key}`, value)
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold mb-2">Monitoring e Notifiche</h2>
        <p className="text-muted">
          Configura gli intervalli di monitoraggio e le notifiche per eventi chiave
        </p>
      </div>

      {/* Monitoring Intervals */}
      <Card className="p-6 space-y-6">
        <h3 className="text-lg font-semibold">Intervalli di Monitoraggio</h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Greeks Snapshot Interval */}
          <FieldWithTooltip
            label="Snapshot Greeks (minuti)"
            tooltip={{
              description:
                'Frequenza di salvataggio snapshot dei Greeks del portfolio (delta, theta, gamma, vega).',
              example: '15 min (default), 30 min (less frequent), 5 min (high frequency)',
              warning:
                'Intervalli troppo bassi (<5min) possono generare molti dati. Intervalli troppo alti (>60min) riducono la granularità storica.',
            }}
          >
            <input
              type="range"
              min={5}
              max={60}
              step={5}
              value={greeksInterval}
              onChange={(e) =>
                setField('monitoring.greeks_snapshot_interval_minutes', parseInt(e.target.value, 10))
              }
              className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer
                [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:h-5
                [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-[var(--wz-amber)]
                [&::-webkit-slider-thumb]:cursor-grab [&::-webkit-slider-thumb]:hover:scale-110
                [&::-moz-range-thumb]:appearance-none [&::-moz-range-thumb]:w-5 [&::-moz-range-thumb]:h-5
                [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-[var(--wz-amber)]
                [&::-moz-range-thumb]:cursor-grab [&::-moz-range-thumb]:hover:scale-110"
            />
            <div className="text-center text-lg font-mono font-bold text-[var(--wz-text)] mt-3">
              {greeksInterval} minuti
            </div>
            <div className="text-center text-sm text-[var(--wz-muted)]">Range: 5 – 60 minuti</div>
          </FieldWithTooltip>

          {/* Risk Check Interval */}
          <FieldWithTooltip
            label="Risk Check (minuti)"
            tooltip={{
              description:
                'Frequenza di esecuzione dei check di rischio (stop loss, hard stops, max days).',
              example: '5 min (default), 10 min (moderate), 1 min (high frequency)',
              warning:
                'Intervalli troppo bassi (<1min) possono causare overhead. Intervalli troppo alti (>30min) ritardano le uscite automatiche.',
            }}
          >
            <input
              type="range"
              min={1}
              max={30}
              step={1}
              value={riskCheckInterval}
              onChange={(e) =>
                setField('monitoring.risk_check_interval_minutes', parseInt(e.target.value, 10))
              }
              className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer
                [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:h-5
                [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-[var(--wz-amber)]
                [&::-webkit-slider-thumb]:cursor-grab [&::-webkit-slider-thumb]:hover:scale-110
                [&::-moz-range-thumb]:appearance-none [&::-moz-range-thumb]:w-5 [&::-moz-range-thumb]:h-5
                [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-[var(--wz-amber)]
                [&::-moz-range-thumb]:cursor-grab [&::-moz-range-thumb]:hover:scale-110"
            />
            <div className="text-center text-lg font-mono font-bold text-[var(--wz-text)] mt-3">
              {riskCheckInterval} minuti
            </div>
            <div className="text-center text-sm text-[var(--wz-muted)]">Range: 1 – 30 minuti</div>
          </FieldWithTooltip>
        </div>
      </Card>

      {/* Notifications */}
      <Card className="p-6 space-y-6">
        <h3 className="text-lg font-semibold">Notifiche</h3>
        <p className="text-sm text-[var(--wz-muted)]">
          Seleziona quali eventi devono generare notifiche (email, Telegram, Discord)
        </p>

        {/* Hard Stop Warning Banner */}
        {showHardStopWarning && (
          <div className="p-4 rounded-lg bg-[var(--wz-error)]/10 border-2 border-[var(--wz-error)]/30 animate-pulse">
            <div className="flex items-start gap-3">
              <ExclamationTriangleIcon className="w-6 h-6 text-[var(--wz-error)] flex-shrink-0" />
              <div>
                <div className="font-semibold text-[var(--wz-error)] mb-1">
                  Hard Stop Triggered NON può essere disabilitato
                </div>
                <p className="text-sm text-[var(--wz-muted)]">
                  Le notifiche per hard stop sono obbligatorie per sicurezza. Toggle ripristinato
                  automaticamente.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Notification Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {NOTIFICATION_TOGGLES.map((toggle) => {
            const isEnabled = notifications[toggle.key] ?? true

            return (
              <label
                key={toggle.key}
                className={`
                  flex items-start gap-3 p-4 rounded-lg border-2 cursor-pointer transition-all
                  ${
                    isEnabled
                      ? 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)]'
                      : 'border-[var(--wz-border)] bg-[var(--wz-surface)] hover:border-[var(--wz-muted)]'
                  }
                  ${!toggle.disableable ? 'border-[var(--wz-error)]/30' : ''}
                `}
              >
                <input
                  type="checkbox"
                  checked={isEnabled}
                  onChange={(e) => handleNotificationToggle(toggle.key, e.target.checked)}
                  disabled={!toggle.disableable}
                  className="mt-1 w-5 h-5 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)] rounded disabled:opacity-50 disabled:cursor-not-allowed"
                />
                <div className="flex-1">
                  <div className="flex items-center gap-2">
                    <span className="font-semibold text-[var(--wz-text)]">{toggle.label}</span>
                    {!toggle.disableable && (
                      <span className="px-2 py-0.5 text-xs rounded bg-[var(--wz-error)]/20 text-[var(--wz-error)] font-semibold">
                        Obbligatorio
                      </span>
                    )}
                  </div>
                  <p className="text-sm text-[var(--wz-muted)] mt-1">{toggle.description}</p>
                </div>
              </label>
            )
          })}
        </div>
      </Card>

      {/* Summary */}
      <Card className="p-4 bg-blue-500/10 border-blue-500/20">
        <p className="text-sm">
          <strong>Configurazione attuale:</strong> Snapshot Greeks ogni {greeksInterval} min,
          Risk Check ogni {riskCheckInterval} min,{' '}
          {NOTIFICATION_TOGGLES.filter((t) => notifications[t.key] ?? true).length} /{' '}
          {NOTIFICATION_TOGGLES.length} notifiche attive
        </p>
      </Card>
    </div>
  )
}
