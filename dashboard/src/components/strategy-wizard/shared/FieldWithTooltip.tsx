/**
 * FieldWithTooltip — Form Field with Label, Tooltip, and Validation
 *
 * Provides consistent field layout with:
 * - Label (with optional asterisk for required fields)
 * - Tooltip icon with popover (description, example, warning)
 * - Field content (passed as children)
 * - Error/warning messages
 */

import type { ReactNode } from 'react'
import * as Popover from '@radix-ui/react-popover'
import { QuestionMarkCircleIcon, ExclamationTriangleIcon } from '@heroicons/react/24/outline'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

export interface TooltipContent {
  description: string
  example?: string
  warning?: string
}

export interface FieldWithTooltipProps {
  label: string
  required?: boolean
  tooltip: TooltipContent
  error?: string
  warning?: string
  children: ReactNode
  className?: string
}

// ============================================================================
// COMPONENT
// ============================================================================

export function FieldWithTooltip({
  label,
  required = false,
  tooltip,
  error,
  warning,
  children,
  className = '',
}: FieldWithTooltipProps) {
  return (
    <div className={`space-y-2 ${className}`}>
      {/* Label Row */}
      <div className="flex items-center gap-2">
        <label className="wz-label text-[var(--wz-text)]">
          {label}
          {required && <span className="text-[var(--wz-error)] ml-1" aria-label="required">*</span>}
        </label>

        {/* Tooltip Popover */}
        <Popover.Root>
          <Popover.Trigger asChild>
            <button
              type="button"
              className="text-[var(--wz-muted)] hover:text-[var(--wz-amber)] transition-colors"
              aria-label={`Help for ${label}`}
            >
              <QuestionMarkCircleIcon className="w-4 h-4" />
            </button>
          </Popover.Trigger>

          <Popover.Portal>
            <Popover.Content
              className="
                z-50 w-80 rounded-lg border border-[var(--wz-border)]
                bg-[var(--wz-elevated)] p-4 shadow-lg
                animate-in fade-in-0 zoom-in-95
                data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95
              "
              sideOffset={5}
              align="start"
            >
              {/* Description */}
              <div className="space-y-3">
                <p className="text-sm text-[var(--wz-text)] leading-relaxed">
                  {tooltip.description}
                </p>

                {/* Example */}
                {tooltip.example && (
                  <div className="pt-2 border-t border-[var(--wz-border)]">
                    <p className="text-xs text-[var(--wz-muted)] mb-1 font-semibold">Example:</p>
                    <code className="block text-xs font-mono bg-[var(--wz-surface)] px-2 py-1 rounded text-[var(--wz-amber)]">
                      {tooltip.example}
                    </code>
                  </div>
                )}

                {/* Warning */}
                {tooltip.warning && (
                  <div className="flex gap-2 pt-2 border-t border-[var(--wz-border)]">
                    <ExclamationTriangleIcon className="w-4 h-4 text-[var(--wz-warning)] flex-shrink-0 mt-0.5" />
                    <p className="text-xs text-[var(--wz-warning)] leading-relaxed">
                      {tooltip.warning}
                    </p>
                  </div>
                )}
              </div>

              <Popover.Arrow className="fill-[var(--wz-elevated)]" />
            </Popover.Content>
          </Popover.Portal>
        </Popover.Root>
      </div>

      {/* Field Content */}
      <div>{children}</div>

      {/* Error Message */}
      {error && (
        <div className="flex items-start gap-2 text-sm text-[var(--wz-error)]" role="alert">
          <ExclamationTriangleIcon className="w-4 h-4 flex-shrink-0 mt-0.5" />
          <span>{error}</span>
        </div>
      )}

      {/* Warning Message */}
      {warning && !error && (
        <div className="flex items-start gap-2 text-sm text-[var(--wz-warning)]" role="alert">
          <ExclamationTriangleIcon className="w-4 h-4 flex-shrink-0 mt-0.5" />
          <span>{warning}</span>
        </div>
      )}
    </div>
  )
}
