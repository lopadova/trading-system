/**
 * StepSummaryCard — Summary Card for Each Wizard Step
 *
 * Displays step status, preview of key values, and edit button for navigation.
 * Used in Step 10 Review to show summary of all completed steps.
 */

import { useWizardStore } from '../../../stores/wizardStore'

export type StepStatus = 'ok' | 'warning' | 'error' | 'incomplete'

export interface StepSummaryCardProps {
  stepNumber: number
  stepName: string
  icon?: string
  status: StepStatus
  preview: string[]
}

export default function StepSummaryCard({
  stepNumber,
  stepName,
  icon = '📋',
  status,
  preview,
}: StepSummaryCardProps) {
  const goToStep = useWizardStore((state) => state.goToStep)
  const stepErrors = useWizardStore((state) => state.stepErrors[stepNumber]) || []

  const errorCount = stepErrors.filter((e) => e.severity === 'error').length
  const warningCount = stepErrors.filter((e) => e.severity === 'warning').length

  // Derive status badge
  const getStatusBadge = () => {
    if (errorCount > 0) {
      return (
        <span className="px-2 py-1 text-xs font-semibold text-red-700 bg-red-100 rounded-md">
          ✗ {errorCount} errori
        </span>
      )
    }
    if (warningCount > 0) {
      return (
        <span className="px-2 py-1 text-xs font-semibold text-yellow-700 bg-yellow-100 rounded-md">
          ⚠️ {warningCount} warning
        </span>
      )
    }
    if (status === 'ok') {
      return (
        <span className="px-2 py-1 text-xs font-semibold text-green-700 bg-green-100 rounded-md">
          ✅ OK
        </span>
      )
    }
    return (
      <span className="px-2 py-1 text-xs font-semibold text-gray-500 bg-gray-100 rounded-md">
        — non compilato
      </span>
    )
  }

  const handleEdit = () => {
    goToStep(stepNumber)
  }

  return (
    <div className="p-4 bg-gray-800 border border-gray-700 rounded-lg shadow-sm hover:border-amber-500 transition-colors">
      {/* Header */}
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <span className="text-2xl">{icon}</span>
          <div>
            <h3 className="text-sm font-semibold text-gray-200">
              Step {stepNumber}: {stepName}
            </h3>
          </div>
        </div>
        {getStatusBadge()}
      </div>

      {/* Preview Values */}
      <div className="mb-3 space-y-1">
        {preview.length > 0 ? (
          preview.map((line, idx) => (
            <p key={idx} className="text-sm text-gray-400">
              {line}
            </p>
          ))
        ) : (
          <p className="text-sm italic text-gray-500">Nessun valore impostato</p>
        )}
      </div>

      {/* Edit Button */}
      <button
        type="button"
        onClick={handleEdit}
        className="w-full px-3 py-2 text-sm font-medium text-amber-300 bg-gray-900 border border-amber-600 rounded-md hover:bg-amber-600 hover:text-white transition-colors"
      >
        Modifica →
      </button>
    </div>
  )
}
