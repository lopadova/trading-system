/**
 * Strategy Wizard Page
 *
 * Main wizard page for creating/editing strategies.
 * Supports multiple modes: new, import, edit, convert.
 */

import { useEffect } from 'react'
import { useWizardStore } from '../stores/wizardStore'
import { LegsStep } from '../components/strategy-wizard/steps/LegsStep'

interface StrategyWizardPageProps {
  mode?: 'new' | 'edit'
  strategyId?: string
  step?: number
}

export function StrategyWizardPage({ mode = 'new', strategyId, step = 1 }: StrategyWizardPageProps) {
  const { goToStep, resetWizard, currentStep, totalSteps } = useWizardStore()

  useEffect(() => {
    // Initialize wizard on mount
    if (mode === 'new') {
      resetWizard()
    }

    // Navigate to specified step if provided
    if (step && step !== 1) {
      goToStep(step)
    }
  }, [mode, strategyId, step, goToStep, resetWizard])

  return (
    <div className="wizard-root">
      <div className="container mx-auto px-4 py-8">
        <h1 className="text-3xl font-bold mb-6">
          {mode === 'new' ? 'Nuova Strategia' : 'Modifica Strategia'}
        </h1>

        {/* Step indicator placeholder */}
        <div className="mb-6 text-sm text-muted">
          Step {currentStep} of {totalSteps}
        </div>

        {/* Render appropriate step component based on currentStep */}
        <div className="bg-card p-6 rounded-lg shadow">
          {currentStep === 5 ? (
            <LegsStep />
          ) : (
            <>
              <p className="text-muted">
                Wizard step {currentStep} implementation coming in next tasks
              </p>
              <p className="text-sm text-muted mt-2">Mode: {mode}</p>
              {strategyId && <p className="text-sm text-muted">Strategy ID: {strategyId}</p>}
            </>
          )}
        </div>
      </div>
    </div>
  )
}
