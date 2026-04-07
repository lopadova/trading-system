/**
 * WizardContainer — Main Layout for Strategy Wizard
 *
 * Responsive 3-column layout:
 * - Desktop (≥1024px): [StepIndicator | Content | HelpPanel]
 * - Tablet (768-1023px): [StepIndicator (collapsible) | Content]
 * - Mobile (<768px): Stacked [StepIndicator (horizontal bar) | Content]
 *
 * Uses framer-motion AnimatePresence for smooth step transitions.
 */

import { useState } from 'react'
import type { ReactNode } from 'react'
import { AnimatePresence, motion } from 'motion/react'
import { Bars3Icon, XMarkIcon, QuestionMarkCircleIcon } from '@heroicons/react/24/outline'
import { StepIndicator } from './StepIndicator'
import { useWizardStore } from '../../stores/wizardStore'
import '../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

interface WizardContainerProps {
  children: ReactNode
  helpContent?: ReactNode
}

// ============================================================================
// ANIMATION VARIANTS
// ============================================================================

const contentVariants = {
  initial: {
    opacity: 0,
    x: 30,
  },
  animate: {
    opacity: 1,
    x: 0,
  },
  exit: {
    opacity: 0,
    x: -30,
  },
}

const transition = {
  duration: 0.25,
}

// ============================================================================
// COMPONENT
// ============================================================================

export function WizardContainer({ children, helpContent }: WizardContainerProps) {
  const currentStep = useWizardStore((state) => state.currentStep)

  // Mobile/Tablet sidebar toggle
  const [isSidebarOpen, setIsSidebarOpen] = useState(false)
  const [isHelpOpen, setIsHelpOpen] = useState(false)

  return (
    <div className="wizard-root min-h-screen bg-[var(--wz-bg)] text-[var(--wz-text)]">
      {/* Mobile Header Bar */}
      <div className="lg:hidden sticky top-0 z-50 bg-[var(--wz-surface)] border-b border-[var(--wz-border)] px-4 py-3 flex items-center justify-between">
        <button
          type="button"
          onClick={() => setIsSidebarOpen(!isSidebarOpen)}
          className="p-2 rounded-lg hover:bg-[var(--wz-elevated)] transition-colors"
          aria-label="Toggle step navigation"
        >
          {isSidebarOpen ? (
            <XMarkIcon className="w-6 h-6" />
          ) : (
            <Bars3Icon className="w-6 h-6" />
          )}
        </button>

        <h1 className="font-display text-lg">
          Strategy Wizard <span className="text-[var(--wz-muted)]">— Step {currentStep}/10</span>
        </h1>

        {helpContent && (
          <button
            type="button"
            onClick={() => setIsHelpOpen(!isHelpOpen)}
            className="p-2 rounded-lg hover:bg-[var(--wz-elevated)] transition-colors"
            aria-label="Toggle help panel"
          >
            <QuestionMarkCircleIcon className="w-6 h-6 text-[var(--wz-amber)]" />
          </button>
        )}
      </div>

      {/* Main Layout */}
      <div className="flex h-[calc(100vh-64px)] lg:h-screen">
        {/* Left Sidebar - Step Indicator */}
        <aside
          className={`
            fixed lg:static inset-y-0 left-0 z-40
            w-64 bg-[var(--wz-surface)] border-r border-[var(--wz-border)]
            transform transition-transform duration-300 ease-in-out
            lg:transform-none
            ${isSidebarOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0'}
            overflow-y-auto
          `}
        >
          {/* Desktop Header */}
          <div className="hidden lg:block p-6 border-b border-[var(--wz-border)]">
            <h1 className="font-display text-xl text-[var(--wz-amber)]">Strategy Wizard</h1>
            <p className="text-sm text-[var(--wz-muted)] mt-1">Step {currentStep} of 10</p>
          </div>

          {/* Step Indicator */}
          <div className="p-4">
            <StepIndicator />
          </div>
        </aside>

        {/* Mobile Sidebar Overlay */}
        {isSidebarOpen && (
          <div
            className="lg:hidden fixed inset-0 bg-black bg-opacity-50 z-30"
            onClick={() => setIsSidebarOpen(false)}
            aria-hidden="true"
          />
        )}

        {/* Center - Content Area */}
        <main className="flex-1 overflow-y-auto">
          <div className="max-w-4xl mx-auto p-6 lg:p-8">
            <AnimatePresence mode="wait" initial={false}>
              <motion.div
                key={currentStep}
                variants={contentVariants}
                initial="initial"
                animate="animate"
                exit="exit"
                transition={transition}
              >
                {children}
              </motion.div>
            </AnimatePresence>
          </div>
        </main>

        {/* Right Sidebar - Help Panel (Desktop Only) */}
        {helpContent && (
          <>
            {/* Desktop Help Panel */}
            <aside className="hidden xl:block w-80 bg-[var(--wz-elevated)] border-l border-[var(--wz-border)] overflow-y-auto">
              <div className="p-6">
                <div className="flex items-center gap-2 mb-4">
                  <QuestionMarkCircleIcon className="w-5 h-5 text-[var(--wz-amber)]" />
                  <h2 className="font-display text-sm font-semibold">Contextual Help</h2>
                </div>
                <div className="text-sm text-[var(--wz-muted)] space-y-4">
                  {helpContent}
                </div>
              </div>
            </aside>

            {/* Mobile/Tablet Help Drawer */}
            {isHelpOpen && (
              <>
                <div
                  className="xl:hidden fixed inset-0 bg-black bg-opacity-50 z-40"
                  onClick={() => setIsHelpOpen(false)}
                  aria-hidden="true"
                />
                <div className="xl:hidden fixed inset-x-0 bottom-0 z-50 bg-[var(--wz-elevated)] border-t border-[var(--wz-border)] max-h-[60vh] overflow-y-auto rounded-t-2xl">
                  <div className="p-6">
                    <div className="flex items-center justify-between mb-4">
                      <div className="flex items-center gap-2">
                        <QuestionMarkCircleIcon className="w-5 h-5 text-[var(--wz-amber)]" />
                        <h2 className="font-display text-sm font-semibold">Help</h2>
                      </div>
                      <button
                        type="button"
                        onClick={() => setIsHelpOpen(false)}
                        className="p-1 rounded hover:bg-[var(--wz-surface)]"
                        aria-label="Close help"
                      >
                        <XMarkIcon className="w-5 h-5" />
                      </button>
                    </div>
                    <div className="text-sm text-[var(--wz-muted)] space-y-4">
                      {helpContent}
                    </div>
                  </div>
                </div>
              </>
            )}
          </>
        )}
      </div>
    </div>
  )
}
