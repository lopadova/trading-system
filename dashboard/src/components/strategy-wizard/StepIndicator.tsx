/**
 * StepIndicator — Vertical Step Navigation for Strategy Wizard
 *
 * Displays 10 wizard steps with status indicators (pending/active/done/error).
 * Active step has amber glow + pulse animation.
 * Completed steps are clickable for navigation.
 */

import { CheckIcon, XMarkIcon } from '@heroicons/react/24/solid'
import { useWizardStore } from '../../stores/wizardStore'
import '../../styles/wizard.css'

// ============================================================================
// CONSTANTS
// ============================================================================

const STEPS = [
  { n: 1, label: 'Identità' },
  { n: 2, label: 'Strumento' },
  { n: 3, label: 'Filtri Ingresso' },
  { n: 4, label: 'Regole Campagna' },
  { n: 5, label: 'Struttura Legs' },
  { n: 6, label: 'Filtri Selezione' },
  { n: 7, label: 'Regole Uscita' },
  { n: 8, label: 'Esecuzione' },
  { n: 9, label: 'Monitoring' },
  { n: 10, label: 'Review & Publish' },
] as const

type StepStatus = 'pending' | 'active' | 'done' | 'error'

// ============================================================================
// HELPERS
// ============================================================================

function getStepStatus(
  stepNumber: number,
  currentStep: number,
  visitedSteps: number[],
  stepErrors: Record<number, unknown[]>
): StepStatus {
  if (stepNumber === currentStep) {
    return 'active'
  }

  const hasErrors = (stepErrors[stepNumber]?.length ?? 0) > 0
  if (hasErrors) {
    return 'error'
  }

  const isVisited = visitedSteps.includes(stepNumber)
  if (isVisited && stepNumber < currentStep) {
    return 'done'
  }

  return 'pending'
}

function isStepClickable(status: StepStatus, stepNumber: number, currentStep: number): boolean {
  // Active step is always clickable
  if (stepNumber === currentStep) {
    return true
  }

  // Completed steps (done) are clickable
  if (status === 'done') {
    return true
  }

  // Error steps are clickable (to fix errors)
  if (status === 'error') {
    return true
  }

  // Pending steps not yet visited are not clickable
  return false
}

// ============================================================================
// COMPONENT
// ============================================================================

interface StepIndicatorProps {
  className?: string
}

export function StepIndicator({ className = '' }: StepIndicatorProps) {
  const currentStep = useWizardStore((state) => state.currentStep)
  const visitedSteps = useWizardStore((state) => state.visitedSteps)
  const stepErrors = useWizardStore((state) => state.stepErrors)
  const goToStep = useWizardStore((state) => state.goToStep)

  return (
    <nav className={`wizard-step-indicator ${className}`} aria-label="Wizard steps">
      <ol className="space-y-2">
        {STEPS.map((step, index) => {
          const status = getStepStatus(step.n, currentStep, visitedSteps, stepErrors)
          const clickable = isStepClickable(status, step.n, currentStep)
          const isLast = index === STEPS.length - 1

          return (
            <li key={step.n} className="relative">
              {/* Step Item */}
              <button
                type="button"
                onClick={() => clickable && goToStep(step.n)}
                disabled={!clickable}
                className={`
                  w-full flex items-center gap-3 p-3 rounded-lg
                  transition-all duration-200
                  ${clickable ? 'cursor-pointer hover:bg-[var(--wz-surface)]' : 'cursor-not-allowed opacity-60'}
                  ${status === 'active' ? 'bg-[var(--wz-amber-dim)]' : ''}
                `}
                aria-current={status === 'active' ? 'step' : undefined}
                aria-label={`Step ${step.n}: ${step.label} (${status})`}
              >
                {/* Step Circle */}
                <div className="relative flex-shrink-0">
                  <div
                    className={`
                      w-10 h-10 rounded-full flex items-center justify-center
                      font-mono font-bold text-sm
                      transition-all duration-300
                      ${getCircleStyles(status)}
                    `}
                  >
                    {status === 'done' && (
                      <CheckIcon className="w-5 h-5 text-white" aria-hidden="true" />
                    )}
                    {status === 'error' && (
                      <XMarkIcon className="w-5 h-5 text-white" aria-hidden="true" />
                    )}
                    {(status === 'pending' || status === 'active') && (
                      <span className={status === 'active' ? 'text-white' : 'text-[var(--wz-muted)]'}>
                        {step.n}
                      </span>
                    )}
                  </div>

                  {/* Active Step Glow */}
                  {status === 'active' && (
                    <div
                      className="absolute inset-0 rounded-full bg-[var(--wz-amber)] opacity-20 blur-md wz-pulse"
                      aria-hidden="true"
                    />
                  )}
                </div>

                {/* Step Label */}
                <span
                  className={`
                    font-display text-sm
                    ${status === 'active' ? 'text-[var(--wz-text)] font-semibold' : ''}
                    ${status === 'done' ? 'text-[var(--wz-text)]' : ''}
                    ${status === 'error' ? 'text-[var(--wz-error)]' : ''}
                    ${status === 'pending' ? 'text-[var(--wz-muted)]' : ''}
                  `}
                >
                  {step.label}
                </span>
              </button>

              {/* Connector Line */}
              {!isLast && (
                <div
                  className={`
                    absolute left-8 top-[58px] w-0.5 h-6
                    transition-colors duration-300
                    ${status === 'done' || status === 'active' ? 'bg-[var(--wz-amber)]' : 'bg-[var(--wz-border)]'}
                  `}
                  aria-hidden="true"
                />
              )}
            </li>
          )
        })}
      </ol>
    </nav>
  )
}

// ============================================================================
// STYLE HELPERS
// ============================================================================

function getCircleStyles(status: StepStatus): string {
  switch (status) {
    case 'pending':
      return 'border-2 border-[var(--wz-border)] bg-transparent'

    case 'active':
      return 'border-2 border-[var(--wz-amber)] bg-[var(--wz-amber-dim)] shadow-[0_0_20px_rgba(245,158,11,0.3)]'

    case 'done':
      return 'border-2 border-[var(--wz-success)] bg-[var(--wz-success)]'

    case 'error':
      return 'border-2 border-[var(--wz-error)] bg-[var(--wz-error)]'

    default:
      return ''
  }
}
