/**
 * Step08ExecutionRules Component
 *
 * Wizard Step 8: Execution Rules
 *
 * Configures order execution parameters:
 * - Order type (limit_mid, limit_ask, limit_bid, market)
 * - Repricing configuration (enabled/disabled with all sub-fields)
 * - Opening sequence
 * - Margin buffer
 * - What-if check
 * - GTC target order
 */

import { useWizardStore } from '../../../stores/wizardStore'
import { FieldWithTooltip } from '../shared/FieldWithTooltip'
import { Card } from '../../ui/Card'
import { ExclamationTriangleIcon } from '@heroicons/react/24/outline'

// ============================================================================
// CONSTANTS
// ============================================================================

const ORDER_TYPES = [
  {
    value: 'limit_mid' as const,
    label: 'Limit Mid',
    description: 'Ordine limit al mid price (bid+ask)/2',
    recommended: true,
    warning: false,
  },
  {
    value: 'limit_ask' as const,
    label: 'Limit Ask',
    description: 'Ordine limit all\'ask (acquisto immediato)',
    recommended: false,
    warning: false,
  },
  {
    value: 'limit_bid' as const,
    label: 'Limit Bid',
    description: 'Ordine limit al bid (vendita immediata)',
    recommended: false,
    warning: false,
  },
  {
    value: 'market' as const,
    label: 'Market',
    description: 'Ordine market (fill immediato, alto slippage)',
    recommended: false,
    warning: true,
  },
] as const

const REPRICING_FALLBACKS = [
  { value: 'cancel_and_block', label: 'Cancel and Block' },
  { value: 'market', label: 'Market Order' },
  { value: 'cancel_and_alert', label: 'Cancel and Alert' },
] as const

const OPENING_SEQUENCES = [
  { value: 'combo_first', label: 'Combo First (then hedge)' },
  { value: 'hedge_first', label: 'Hedge First (then combo)' },
  { value: 'simultaneous', label: 'Simultaneous' },
] as const

// ============================================================================
// COMPONENT
// ============================================================================

export function Step08ExecutionRules() {
  const { draft, setField } = useWizardStore()

  // Extract current values with defaults
  const orderType = draft.execution_rules?.order_type ?? 'limit_mid'
  const repricingEnabled = draft.execution_rules?.repricing?.enabled ?? false
  const repricingMaxAttempts = draft.execution_rules?.repricing?.max_attempts ?? 5
  const repricingIntervalSeconds = draft.execution_rules?.repricing?.interval_seconds ?? 30
  const repricingStepPct = draft.execution_rules?.repricing?.step_pct_of_half_spread ?? 10
  const repricingMaxSlippagePct = draft.execution_rules?.repricing?.max_slippage_pct_from_first_mid ?? 5
  const repricingFallback = draft.execution_rules?.repricing?.fallback_on_max_attempts ?? 'cancel_and_block'
  const openingSequence = draft.execution_rules?.opening_sequence ?? 'combo_first'
  const marginBufferPct = draft.execution_rules?.margin_buffer_pct ?? 10
  const whatIfCheckEnabled = draft.execution_rules?.what_if_check_enabled ?? true
  const gtcEnabled = draft.execution_rules?.gtc_target_order?.enabled ?? false
  const gtcSubmitImmediately = draft.execution_rules?.gtc_target_order?.submit_immediately_after_fill ?? true

  const showMarketWarning = orderType === 'market'

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold mb-2">Regole di Esecuzione</h2>
        <p className="text-muted">
          Configura come vengono eseguiti gli ordini: tipo, repricing, sequenza di apertura
        </p>
      </div>

      {/* Order Type */}
      <Card className="p-6 space-y-6">
        <FieldWithTooltip
          label="Tipo di Ordine"
          required
          tooltip={{
            description:
              'Tipo di ordine usato per l\'entrata. Limit Mid è raccomandato per bilanciare fill rate e slippage.',
            example: 'limit_mid per SPX options, limit_ask per fill garantito ma costoso',
            warning:
              'Market orders su SPX possono avere slippage 5-10%. Usare solo in casi eccezionali.',
          }}
        >
          <div className="space-y-3">
            {ORDER_TYPES.map((type) => (
              <label
                key={type.value}
                className={`
                  block p-4 rounded-lg border-2 cursor-pointer transition-all
                  ${
                    orderType === type.value
                      ? 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)]'
                      : 'border-[var(--wz-border)] bg-[var(--wz-surface)] hover:border-[var(--wz-muted)]'
                  }
                  ${type.warning ? 'border-[var(--wz-error)]/30' : ''}
                `}
              >
                <div className="flex items-start gap-3">
                  <input
                    type="radio"
                    name="order_type"
                    value={type.value}
                    checked={orderType === type.value}
                    onChange={(e) => setField('execution_rules.order_type', e.target.value)}
                    className="mt-1 w-4 h-4 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]"
                  />
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-[var(--wz-text)]">{type.label}</span>
                      {type.recommended && (
                        <span className="px-2 py-0.5 text-xs rounded bg-[var(--wz-success)]/20 text-[var(--wz-success)] font-semibold">
                          Raccomandato
                        </span>
                      )}
                      {type.warning && (
                        <span className="px-2 py-0.5 text-xs rounded bg-[var(--wz-error)]/20 text-[var(--wz-error)] font-semibold">
                          Non Raccomandato
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-[var(--wz-muted)] mt-1">{type.description}</p>
                  </div>
                </div>
              </label>
            ))}
          </div>

          {/* Market Order Warning */}
          {showMarketWarning && (
            <div className="mt-4 p-4 rounded-lg bg-[var(--wz-error)]/10 border-2 border-[var(--wz-error)]/30">
              <div className="flex items-start gap-3">
                <ExclamationTriangleIcon className="w-6 h-6 text-[var(--wz-error)] flex-shrink-0" />
                <div>
                  <div className="font-semibold text-[var(--wz-error)] mb-1">
                    Attenzione: Market Order
                  </div>
                  <p className="text-sm text-[var(--wz-muted)]">
                    Gli ordini market su opzioni SPX possono avere slippage 5-10% o superiore.
                    Raccomandato solo per chiusure d'emergenza, non per aperture.
                  </p>
                </div>
              </div>
            </div>
          )}
        </FieldWithTooltip>
      </Card>

      {/* Repricing Configuration */}
      <Card className="p-6 space-y-6">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold">Repricing Automatico</h3>
            <p className="text-sm text-[var(--wz-muted)]">
              Riprezza automaticamente ordini non fillati per migliorare fill rate
            </p>
          </div>
          <label className="relative inline-flex items-center cursor-pointer">
            <input
              type="checkbox"
              checked={repricingEnabled}
              onChange={(e) => setField('execution_rules.repricing.enabled', e.target.checked)}
              className="sr-only peer"
            />
            <div className="w-11 h-6 bg-[var(--wz-surface)] peer-focus:outline-none peer-focus:ring-4 peer-focus:ring-[var(--wz-amber-dim)] rounded-full peer peer-checked:after:translate-x-full peer-checked:after:border-white after:content-[''] after:absolute after:top-[2px] after:left-[2px] after:bg-white after:border-gray-300 after:border after:rounded-full after:h-5 after:w-5 after:transition-all peer-checked:bg-[var(--wz-amber)]"></div>
          </label>
        </div>

        {/* Repricing Fields (disabled if repricing not enabled) */}
        <div className={`space-y-4 ${!repricingEnabled ? 'opacity-50' : ''}`}>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Max Attempts */}
            <FieldWithTooltip
              label="Tentativi Massimi"
              tooltip={{
                description: 'Numero massimo di tentativi di repricing prima del fallback.',
                example: '5 (moderate), 10 (aggressive)',
              }}
            >
              <input
                type="number"
                min={1}
                max={10}
                step={1}
                value={repricingMaxAttempts}
                onChange={(e) =>
                  setField('execution_rules.repricing.max_attempts', parseInt(e.target.value, 10) || 1)
                }
                disabled={!repricingEnabled}
                className="
                  w-full px-4 py-2 rounded-lg
                  bg-[var(--wz-surface)] border border-[var(--wz-border)]
                  text-[var(--wz-text)] font-mono
                  focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                  transition-colors
                  disabled:cursor-not-allowed
                "
              />
            </FieldWithTooltip>

            {/* Interval Seconds */}
            <FieldWithTooltip
              label="Intervallo (secondi)"
              tooltip={{
                description: 'Tempo di attesa tra un tentativo di repricing e il successivo.',
                example: '30s (default), 60s (patient)',
              }}
            >
              <input
                type="range"
                min={10}
                max={120}
                step={10}
                value={repricingIntervalSeconds}
                onChange={(e) =>
                  setField('execution_rules.repricing.interval_seconds', parseInt(e.target.value, 10))
                }
                disabled={!repricingEnabled}
                className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer disabled:cursor-not-allowed"
              />
              <div className="text-center text-sm font-mono text-[var(--wz-text)] mt-2">
                {repricingIntervalSeconds}s
              </div>
            </FieldWithTooltip>

            {/* Step % of Half Spread */}
            <FieldWithTooltip
              label="Step (% di Half Spread)"
              tooltip={{
                description: 'Percentuale di half spread da aggiungere/sottrarre ad ogni repricing.',
                example: '10% (conservative), 25% (moderate), 50% (aggressive)',
              }}
            >
              <input
                type="range"
                min={1}
                max={50}
                step={1}
                value={repricingStepPct}
                onChange={(e) =>
                  setField('execution_rules.repricing.step_pct_of_half_spread', parseFloat(e.target.value))
                }
                disabled={!repricingEnabled}
                className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer disabled:cursor-not-allowed"
              />
              <div className="text-center text-sm font-mono text-[var(--wz-text)] mt-2">
                {repricingStepPct}%
              </div>
            </FieldWithTooltip>

            {/* Max Slippage from First Mid */}
            <FieldWithTooltip
              label="Max Slippage (% dal Mid iniziale)"
              tooltip={{
                description: 'Slippage massimo accettabile rispetto al mid price iniziale.',
                example: '5% (tight), 10% (moderate), 20% (wide)',
              }}
            >
              <input
                type="range"
                min={1}
                max={20}
                step={0.5}
                value={repricingMaxSlippagePct}
                onChange={(e) =>
                  setField('execution_rules.repricing.max_slippage_pct_from_first_mid', parseFloat(e.target.value))
                }
                disabled={!repricingEnabled}
                className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer disabled:cursor-not-allowed"
              />
              <div className="text-center text-sm font-mono text-[var(--wz-text)] mt-2">
                {repricingMaxSlippagePct}%
              </div>
            </FieldWithTooltip>
          </div>

          {/* Fallback */}
          <FieldWithTooltip
            label="Fallback se Max Tentativi Raggiunto"
            tooltip={{
              description: 'Azione da intraprendere se nessun fill dopo i tentativi massimi.',
              example: 'cancel_and_block (safe), market (aggressive)',
            }}
          >
            <select
              value={repricingFallback}
              onChange={(e) =>
                setField('execution_rules.repricing.fallback_on_max_attempts', e.target.value)
              }
              disabled={!repricingEnabled}
              className="
                w-full px-4 py-2 rounded-lg
                bg-[var(--wz-surface)] border border-[var(--wz-border)]
                text-[var(--wz-text)]
                focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                transition-colors
                disabled:cursor-not-allowed
              "
            >
              {REPRICING_FALLBACKS.map((fb) => (
                <option key={fb.value} value={fb.value}>
                  {fb.label}
                </option>
              ))}
            </select>
          </FieldWithTooltip>
        </div>
      </Card>

      {/* Other Execution Settings */}
      <Card className="p-6 space-y-6">
        <h3 className="text-lg font-semibold">Altre Impostazioni</h3>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Opening Sequence */}
          <FieldWithTooltip
            label="Sequenza di Apertura"
            tooltip={{
              description: 'Ordine di esecuzione per combo e hedge legs.',
              example: 'combo_first per strategie income, hedge_first per protezione prioritaria',
            }}
          >
            <select
              value={openingSequence}
              onChange={(e) => setField('execution_rules.opening_sequence', e.target.value)}
              className="
                w-full px-4 py-2 rounded-lg
                bg-[var(--wz-surface)] border border-[var(--wz-border)]
                text-[var(--wz-text)]
                focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
                transition-colors
              "
            >
              {OPENING_SEQUENCES.map((seq) => (
                <option key={seq.value} value={seq.value}>
                  {seq.label}
                </option>
              ))}
            </select>
          </FieldWithTooltip>

          {/* Margin Buffer % */}
          <FieldWithTooltip
            label="Margin Buffer (%)"
            tooltip={{
              description: 'Percentuale di margine extra richiesto prima di aprire posizione.',
              example: '10% (default), 20% (conservative), 5% (aggressive)',
            }}
          >
            <input
              type="range"
              min={5}
              max={50}
              step={5}
              value={marginBufferPct}
              onChange={(e) =>
                setField('execution_rules.margin_buffer_pct', parseFloat(e.target.value))
              }
              className="w-full h-2 bg-[var(--wz-surface)] rounded-lg appearance-none cursor-pointer"
            />
            <div className="text-center text-sm font-mono text-[var(--wz-text)] mt-2">
              {marginBufferPct}%
            </div>
          </FieldWithTooltip>
        </div>

        {/* Toggles */}
        <div className="space-y-4">
          {/* What-If Check */}
          <label className="flex items-center justify-between p-4 rounded-lg bg-[var(--wz-surface)] border border-[var(--wz-border)]">
            <div>
              <div className="font-semibold text-[var(--wz-text)]">IBKR What-If Check</div>
              <p className="text-sm text-[var(--wz-muted)]">
                Esegui check IBKR prima di inviare ordine (raccomandato)
              </p>
            </div>
            <input
              type="checkbox"
              checked={whatIfCheckEnabled}
              onChange={(e) =>
                setField('execution_rules.what_if_check_enabled', e.target.checked)
              }
              className="w-5 h-5 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)] rounded"
            />
          </label>

          {/* GTC Target Order */}
          <div className="p-4 rounded-lg bg-[var(--wz-surface)] border border-[var(--wz-border)] space-y-3">
            <label className="flex items-center justify-between">
              <div>
                <div className="font-semibold text-[var(--wz-text)]">GTC Target Order</div>
                <p className="text-sm text-[var(--wz-muted)]">
                  Invia ordine GTC al target dopo il fill di entrata
                </p>
              </div>
              <input
                type="checkbox"
                checked={gtcEnabled}
                onChange={(e) =>
                  setField('execution_rules.gtc_target_order.enabled', e.target.checked)
                }
                className="w-5 h-5 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)] rounded"
              />
            </label>

            {gtcEnabled && (
              <label className="flex items-center justify-between pl-4 pt-2 border-t border-[var(--wz-border)]">
                <div>
                  <div className="font-medium text-[var(--wz-text)]">
                    Invia Immediatamente Dopo Fill
                  </div>
                  <p className="text-xs text-[var(--wz-muted)]">
                    Invia GTC subito dopo il fill (altrimenti attende conferma manuale)
                  </p>
                </div>
                <input
                  type="checkbox"
                  checked={gtcSubmitImmediately}
                  onChange={(e) =>
                    setField('execution_rules.gtc_target_order.submit_immediately_after_fill', e.target.checked)
                  }
                  className="w-5 h-5 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)] rounded"
                />
              </label>
            )}
          </div>
        </div>
      </Card>
    </div>
  )
}
