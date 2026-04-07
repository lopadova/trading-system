/**
 * NavigationButtons — Wizard Step Navigation Footer
 *
 * Features:
 * - Back button (if step > 1)
 * - Step counter (center)
 * - Next/Publish button (if step <= 10)
 * - Progress bar (animated)
 * - Shake animation on validation failure
 */

import { useState, useEffect } from 'react'
import { ArrowLeftIcon, ArrowRightIcon, RocketLaunchIcon } from '@heroicons/react/24/outline'
import { useWizardStore } from '../../../stores/wizardStore'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

export interface NavigationButtonsProps {
  className?: string
}

// ============================================================================
// COMPONENT
// ============================================================================

export function NavigationButtons({ className = '' }: NavigationButtonsProps) {
  const currentStep = useWizardStore((state) => state.currentStep)
  const totalSteps = useWizardStore((state) => state.totalSteps)
  const nextStep = useWizardStore((state) => state.nextStep)
  const prevStep = useWizardStore((state) => state.prevStep)

  const [isShaking, setIsShaking] = useState(false)

  const progress = (currentStep / totalSteps) * 100
  const isFirstStep = currentStep === 1
  const isLastStep = currentStep === totalSteps

  const handleNext = () => {
    const success = nextStep()

    if (!success) {
      // Trigger shake animation on validation failure
      setIsShaking(true)
    }
  }

  // Reset shake animation after it completes
  useEffect(() => {
    if (isShaking) {
      const timer = setTimeout(() => setIsShaking(false), 500)
      return () => clearTimeout(timer)
    }
  }, [isShaking])

  return (
    <div className={`wizard-navigation-footer ${className}`}>
      {/* Progress Bar */}
      <div className="absolute top-0 left-0 w-full h-1 bg-[var(--wz-surface)]">
        <div
          className="h-full bg-[var(--wz-amber)] transition-all duration-500 ease-out"
          style={{ width: `${progress}%` }}
          role="progressbar"
          aria-valuenow={currentStep}
          aria-valuemin={1}
          aria-valuemax={totalSteps}
        />
      </div>

      {/* Footer Content */}
      <div className="relative flex items-center justify-between px-6 py-4 bg-[var(--wz-surface)] border-t border-[var(--wz-border)]">
        {/* Left: Back Button */}
        <div className="flex-1">
          {!isFirstStep && (
            <button
              type="button"
              onClick={prevStep}
              className="
                inline-flex items-center gap-2 px-4 py-2 rounded-lg
                text-[var(--wz-muted)] hover:text-[var(--wz-text)]
                hover:bg-[var(--wz-elevated)]
                transition-all duration-200
                font-medium
              "
            >
              <ArrowLeftIcon className="w-4 h-4" />
              <span>Indietro</span>
            </button>
          )}
        </div>

        {/* Center: Step Counter */}
        <div className="flex-1 flex justify-center">
          <div className="font-display text-sm text-[var(--wz-muted)]">
            Step <span className="text-[var(--wz-amber)] font-bold">{currentStep}</span> di {totalSteps}
          </div>
        </div>

        {/* Right: Next/Publish Button */}
        <div className="flex-1 flex justify-end">
          {!isLastStep ? (
            <button
              type="button"
              onClick={handleNext}
              className={`
                inline-flex items-center gap-2 px-6 py-2.5 rounded-lg
                bg-[var(--wz-amber)] text-[var(--wz-bg)]
                hover:bg-[var(--wz-amber)] hover:brightness-110
                transition-all duration-200
                font-semibold shadow-md hover:shadow-lg
                ${isShaking ? 'wz-shake' : ''}
              `}
            >
              <span>Avanti</span>
              <ArrowRightIcon className="w-4 h-4" />
            </button>
          ) : (
            <button
              type="button"
              onClick={handleNext}
              className={`
                inline-flex items-center gap-2 px-6 py-2.5 rounded-lg
                bg-gradient-to-r from-[var(--wz-amber)] to-[var(--wz-warning)]
                text-white
                hover:brightness-110 hover:scale-105
                transition-all duration-200
                font-semibold shadow-lg
                ${isShaking ? 'wz-shake' : ''}
              `}
            >
              <RocketLaunchIcon className="w-5 h-5" />
              <span>Review & Pubblica</span>
            </button>
          )}
        </div>
      </div>
    </div>
  )
}
