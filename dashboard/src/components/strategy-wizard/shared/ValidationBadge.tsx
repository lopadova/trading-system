/**
 * ValidationBadge — Visual Indicator for Validation Status
 *
 * Displays count of errors/warnings with appropriate styling.
 * Used in step headers and navigation to show validation state.
 */

import { CheckCircleIcon, XCircleIcon, ExclamationTriangleIcon } from '@heroicons/react/24/solid'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

export interface ValidationBadgeProps {
  errorCount?: number
  warningCount?: number
  successMessage?: string
  className?: string
}

// ============================================================================
// COMPONENT
// ============================================================================

export function ValidationBadge({
  errorCount = 0,
  warningCount = 0,
  successMessage,
  className = '',
}: ValidationBadgeProps) {
  // If no errors or warnings, show success
  if (errorCount === 0 && warningCount === 0) {
    if (successMessage) {
      return (
        <div
          className={`
            inline-flex items-center gap-2 px-3 py-1.5 rounded-lg
            bg-[var(--wz-success)] bg-opacity-10
            border border-[var(--wz-success)] border-opacity-30
            text-[var(--wz-success)] text-sm font-medium
            ${className}
          `}
        >
          <CheckCircleIcon className="w-4 h-4" aria-hidden="true" />
          <span>{successMessage}</span>
        </div>
      )
    }
    return null
  }

  // Show errors if present
  if (errorCount > 0) {
    return (
      <div
        className={`
          inline-flex items-center gap-2 px-3 py-1.5 rounded-lg
          bg-[var(--wz-error)] bg-opacity-10
          border border-[var(--wz-error)] border-opacity-30
          text-[var(--wz-error)] text-sm font-medium
          ${className}
        `}
        role="alert"
      >
        <XCircleIcon className="w-4 h-4" aria-hidden="true" />
        <span>
          {errorCount} {errorCount === 1 ? 'Error' : 'Errors'}
        </span>
      </div>
    )
  }

  // Show warnings if present (and no errors)
  if (warningCount > 0) {
    return (
      <div
        className={`
          inline-flex items-center gap-2 px-3 py-1.5 rounded-lg
          bg-[var(--wz-warning)] bg-opacity-10
          border border-[var(--wz-warning)] border-opacity-30
          text-[var(--wz-warning)] text-sm font-medium
          ${className}
        `}
        role="alert"
      >
        <ExclamationTriangleIcon className="w-4 h-4" aria-hidden="true" />
        <span>
          {warningCount} {warningCount === 1 ? 'Warning' : 'Warnings'}
        </span>
      </div>
    )
  }

  return null
}
