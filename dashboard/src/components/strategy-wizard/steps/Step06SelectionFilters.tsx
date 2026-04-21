/**
 * Step06SelectionFilters Component
 *
 * Wizard Step 6: Option Selection Filters
 *
 * Configures filters and scoring for selecting option contracts:
 * - Minimum open interest
 * - Maximum spread as percentage of mid price (color-coded slider)
 * - Scoring method (min_delta_distance, max_oi, min_spread)
 */

import { useWizardStore } from '../../../stores/wizardStore'
import { FieldWithTooltip } from '../shared/FieldWithTooltip'
import { Card } from '../../ui/Card'

// ============================================================================
// CONSTANTS
// ============================================================================

const SCORING_METHODS = [
  {
    value: 'min_delta_distance' as const,
    label: 'Min Delta Distance',
    description: 'Seleziona il contratto con delta più vicino al target (raccomandato)',
    recommended: true,
  },
  {
    value: 'max_oi' as const,
    label: 'Max Open Interest',
    description: 'Preferisce i contratti con maggiore liquidità',
    recommended: false,
  },
  {
    value: 'min_spread' as const,
    label: 'Min Spread',
    description: 'Preferisce i contratti con spread bid-ask più stretto',
    recommended: false,
  },
] as const

// ============================================================================
// HELPERS
// ============================================================================

/**
 * Get track color based on spread percentage
 * Green (0-5%) → Yellow (5-10%) → Red (10-20%)
 */
function getSpreadTrackColor(spreadPct: number): string {
  if (spreadPct <= 5) {
    return 'var(--wz-success)'
  }
  if (spreadPct <= 10) {
    return 'var(--wz-warning)'
  }
  return 'var(--wz-error)'
}

// ============================================================================
// COMPONENT
// ============================================================================

export function Step06SelectionFilters() {
  const { draft, setField } = useWizardStore()

  // Extract current values with defaults
  const minOpenInterest = draft.selection_filters?.min_open_interest ?? 100
  const maxSpreadPct = draft.selection_filters?.max_spread_pct_of_mid ?? 10
  const scoringMethod = draft.selection_filters?.scoring_method ?? 'min_delta_distance'

  // Calculate percentage for spread slider gradient
  const spreadPercentage = (maxSpreadPct / 20) * 100
  const spreadTrackColor = getSpreadTrackColor(maxSpreadPct)

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold mb-2">Filtri Selezione Opzioni</h2>
        <p className="text-muted">
          Configura i criteri per selezionare i contratti opzioni nella catena (chain)
        </p>
      </div>

      {/* Main Card */}
      <Card className="p-6 space-y-6">
        {/* Min Open Interest */}
        <FieldWithTooltip
          label="Minimo Open Interest"
          required
          tooltip={{
            description:
              'Numero minimo di contratti aperti richiesto per considerare un\'opzione. Valori più alti garantiscono liquidità ma riducono le scelte disponibili.',
            example: '100 (default), 500 (conservative), 50 (aggressive)',
            warning:
              'Valori troppo bassi (<50) possono portare a slippage elevato. Valori troppo alti (>1000) possono limitare eccessivamente le opzioni disponibili.',
          }}
        >
          <input
            type="number"
            min={0}
            max={10000}
            step={10}
            value={minOpenInterest}
            onChange={(e) =>
              setField('selection_filters.min_open_interest', parseInt(e.target.value, 10) || 0)
            }
            className="
              w-full px-4 py-2 rounded-lg
              bg-[var(--wz-surface)] border border-[var(--wz-border)]
              text-[var(--wz-text)] font-mono
              focus:outline-none focus:border-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]
              transition-colors
            "
          />
          <div className="mt-2 text-sm text-[var(--wz-muted)]">
            Range: 0 – 10,000 contratti
          </div>
        </FieldWithTooltip>

        {/* Max Spread Percentage */}
        <FieldWithTooltip
          label="Massimo Spread (% del Mid Price)"
          required
          tooltip={{
            description:
              'Spread bid-ask massimo accettabile come percentuale del mid price. Spread più bassi riducono il costo di entrata/uscita.',
            example: '5% (tight), 10% (moderate), 15% (wide)',
            warning:
              'SPX options spesso hanno spread 2-5%. Spread >10% indicano bassa liquidità e possono causare slippage significativo.',
          }}
        >
          {/* Custom Slider */}
          <div className="space-y-4">
            <div className="relative pt-2 pb-2">
              {/* Track Background */}
              <div className="relative h-2 bg-[var(--wz-surface)] rounded-full overflow-hidden">
                {/* Filled Track with Gradient */}
                <div
                  className="absolute h-full rounded-full transition-all duration-150"
                  style={{
                    width: `${spreadPercentage}%`,
                    background: `linear-gradient(to right,
                      var(--wz-success) 0%,
                      var(--wz-success) 25%,
                      var(--wz-warning) 25%,
                      var(--wz-warning) 50%,
                      var(--wz-error) 50%,
                      var(--wz-error) 100%
                    )`,
                  }}
                />
              </div>

              {/* Range Input */}
              <input
                type="range"
                min={0}
                max={20}
                step={0.5}
                value={maxSpreadPct}
                onChange={(e) =>
                  setField('selection_filters.max_spread_pct_of_mid', parseFloat(e.target.value))
                }
                className="absolute top-2 left-0 w-full h-2 appearance-none bg-transparent cursor-pointer
                  [&::-webkit-slider-thumb]:appearance-none [&::-webkit-slider-thumb]:w-5 [&::-webkit-slider-thumb]:h-5
                  [&::-webkit-slider-thumb]:rounded-full [&::-webkit-slider-thumb]:bg-[var(--wz-amber)]
                  [&::-webkit-slider-thumb]:border-2 [&::-webkit-slider-thumb]:border-[var(--wz-bg)]
                  [&::-webkit-slider-thumb]:shadow-lg [&::-webkit-slider-thumb]:cursor-grab
                  [&::-webkit-slider-thumb]:hover:scale-110 [&::-webkit-slider-thumb]:transition-transform
                  [&::-moz-range-thumb]:appearance-none [&::-moz-range-thumb]:w-5 [&::-moz-range-thumb]:h-5
                  [&::-moz-range-thumb]:rounded-full [&::-moz-range-thumb]:bg-[var(--wz-amber)]
                  [&::-moz-range-thumb]:border-2 [&::-moz-range-thumb]:border-[var(--wz-bg)]
                  [&::-moz-range-thumb]:shadow-lg [&::-moz-range-thumb]:cursor-grab
                  [&::-moz-range-thumb]:hover:scale-110 [&::-moz-range-thumb]:transition-transform"
                aria-label="Maximum spread percentage"
              />
            </div>

            {/* Value Display */}
            <div className="flex items-center gap-4">
              <div className="flex-1">
                <div
                  className="text-3xl font-mono font-bold"
                  style={{ color: spreadTrackColor }}
                >
                  {maxSpreadPct.toFixed(1)}%
                </div>
              </div>
              <div className="flex gap-2 text-xs">
                <span className="px-2 py-1 rounded bg-[var(--wz-success)]/20 text-[var(--wz-success)]">
                  0-5% Green
                </span>
                <span className="px-2 py-1 rounded bg-[var(--wz-warning)]/20 text-[var(--wz-warning)]">
                  5-10% Yellow
                </span>
                <span className="px-2 py-1 rounded bg-[var(--wz-error)]/20 text-[var(--wz-error)]">
                  10-20% Red
                </span>
              </div>
            </div>
          </div>
        </FieldWithTooltip>

        {/* Scoring Method */}
        <FieldWithTooltip
          label="Metodo di Scoring"
          required
          tooltip={{
            description:
              'Algoritmo usato per scegliere quale contratto aprire quando più opzioni soddisfano i criteri. Influisce sulla precisione del matching delta vs. liquidità.',
            example: 'min_delta_distance per strategie delta-neutral, max_oi per alta liquidità',
            warning:
              'min_delta_distance è raccomandato per la maggior parte delle strategie. Cambiare solo se hai esigenze specifiche di liquidità o spread.',
          }}
        >
          <div className="space-y-3">
            {SCORING_METHODS.map((method) => (
              <label
                key={method.value}
                className={`
                  block p-4 rounded-lg border-2 cursor-pointer transition-all
                  ${
                    scoringMethod === method.value
                      ? 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)]'
                      : 'border-[var(--wz-border)] bg-[var(--wz-surface)] hover:border-[var(--wz-muted)]'
                  }
                `}
              >
                <div className="flex items-start gap-3">
                  <input
                    type="radio"
                    name="scoring_method"
                    value={method.value}
                    checked={scoringMethod === method.value}
                    onChange={(e) =>
                      setField('selection_filters.scoring_method', e.target.value)
                    }
                    className="mt-1 w-4 h-4 text-[var(--wz-amber)] focus:ring-2 focus:ring-[var(--wz-amber-dim)]"
                  />
                  <div className="flex-1">
                    <div className="flex items-center gap-2">
                      <span className="font-semibold text-[var(--wz-text)]">
                        {method.label}
                      </span>
                      {method.recommended && (
                        <span className="px-2 py-0.5 text-xs rounded bg-[var(--wz-success)]/20 text-[var(--wz-success)] font-semibold">
                          Raccomandato
                        </span>
                      )}
                    </div>
                    <p className="text-sm text-[var(--wz-muted)] mt-1">
                      {method.description}
                    </p>
                  </div>
                </div>
              </label>
            ))}
          </div>
        </FieldWithTooltip>
      </Card>

      {/* Summary Card */}
      <Card className="p-4 bg-blue-500/10 border-blue-500/20">
        <p className="text-sm">
          <strong>Configurazione attuale:</strong> OI ≥ {minOpenInterest} contratti, Spread ≤{' '}
          {maxSpreadPct.toFixed(1)}%, Scoring:{' '}
          {SCORING_METHODS.find((m) => m.value === scoringMethod)?.label}
        </p>
      </Card>
    </div>
  )
}
