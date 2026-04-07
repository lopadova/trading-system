/**
 * ValidationSummary — Clickable List of Validation Errors and Warnings
 *
 * Groups errors/warnings by step and allows clicking to navigate to the step
 * where the error occurs. Used in Step 10 Review.
 */

import { useWizardStore } from '../../../stores/wizardStore'
import type { ValidationError } from '../../../utils/sdf-validator'

export default function ValidationSummary() {
  const globalErrors = useWizardStore((state) => state.globalErrors)
  const goToStep = useWizardStore((state) => state.goToStep)

  if (globalErrors.length === 0) {
    return (
      <div className="p-4 bg-green-900/20 border border-green-700 rounded-lg">
        <div className="flex items-center gap-2">
          <span className="text-2xl">✅</span>
          <p className="text-sm font-medium text-green-300">
            Nessun errore rilevato. La strategia è pronta per la pubblicazione!
          </p>
        </div>
      </div>
    )
  }

  // Group errors by step
  const errorsByStep = globalErrors.reduce(
    (acc, error) => {
      const step = error.step || 0
      if (!acc[step]) {
        acc[step] = []
      }
      acc[step].push(error)
      return acc
    },
    {} as Record<number, ValidationError[]>
  )

  const sortedSteps = Object.keys(errorsByStep)
    .map(Number)
    .sort((a, b) => a - b)

  const handleErrorClick = (step: number) => {
    goToStep(step)
    // TODO: Scroll to the specific field (requires field ID mapping)
  }

  return (
    <div className="space-y-3">
      <h3 className="text-lg font-semibold text-gray-200">Riepilogo Validazione</h3>

      {sortedSteps.map((step) => {
        const errors = errorsByStep[step] || []
        const errorItems = errors.filter((e) => e.severity === 'error')
        const warningItems = errors.filter((e) => e.severity === 'warning')

        return (
          <div key={step} className="p-3 bg-gray-800 border border-gray-700 rounded-lg">
            <h4 className="mb-2 text-sm font-semibold text-gray-300">
              Step {step === 0 ? 'Globale' : step}
            </h4>

            <div className="space-y-2">
              {/* Errors */}
              {errorItems.map((error, idx) => (
                <button
                  key={`error-${idx}`}
                  type="button"
                  onClick={() => handleErrorClick(error.step || 0)}
                  className="w-full text-left p-2 bg-red-900/20 border border-red-700 rounded-md hover:bg-red-900/40 transition-colors group"
                >
                  <div className="flex items-start gap-2">
                    <span className="text-red-400 font-bold">✗</span>
                    <div className="flex-1">
                      <p className="text-sm font-medium text-red-300">
                        {error.field}
                      </p>
                      <p className="text-xs text-red-400">{error.message}</p>
                    </div>
                    <span className="text-xs text-red-500 group-hover:text-red-300 transition-colors">
                      →
                    </span>
                  </div>
                </button>
              ))}

              {/* Warnings */}
              {warningItems.map((warning, idx) => (
                <button
                  key={`warning-${idx}`}
                  type="button"
                  onClick={() => handleErrorClick(warning.step || 0)}
                  className="w-full text-left p-2 bg-yellow-900/20 border border-yellow-700 rounded-md hover:bg-yellow-900/40 transition-colors group"
                >
                  <div className="flex items-start gap-2">
                    <span className="text-yellow-400 font-bold">⚠</span>
                    <div className="flex-1">
                      <p className="text-sm font-medium text-yellow-300">
                        {warning.field}
                      </p>
                      <p className="text-xs text-yellow-400">{warning.message}</p>
                    </div>
                    <span className="text-xs text-yellow-500 group-hover:text-yellow-300 transition-colors">
                      →
                    </span>
                  </div>
                </button>
              ))}
            </div>
          </div>
        )
      })}
    </div>
  )
}
