/**
 * ImportDropzone — JSON File Import Drag & Drop Zone
 *
 * Features:
 * - Drag & drop or click to select JSON file
 * - Visual feedback on hover/drag
 * - Parses JSON and calls wizardStore.initFromJson()
 * - Loading state with spinner
 * - Success/error feedback
 */

import { useState, useRef } from 'react'
import { ArrowUpTrayIcon, CheckCircleIcon, XCircleIcon } from '@heroicons/react/24/outline'
import { useWizardStore } from '../../../stores/wizardStore'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

type DropzoneState = 'idle' | 'hover' | 'loading' | 'success' | 'error'

export interface ImportDropzoneProps {
  onImportSuccess?: () => void
  onImportError?: (errors: string[]) => void
  className?: string
}

// ============================================================================
// COMPONENT
// ============================================================================

export function ImportDropzone({
  onImportSuccess,
  onImportError,
  className = '',
}: ImportDropzoneProps) {
  const [state, setState] = useState<DropzoneState>('idle')
  const [errorMessage, setErrorMessage] = useState<string>('')
  const inputRef = useRef<HTMLInputElement>(null)
  const initFromJson = useWizardStore((state) => state.initFromJson)

  const handleFileSelect = async (file: File) => {
    // Validate file type
    if (!file.name.endsWith('.json')) {
      setState('error')
      setErrorMessage('Formato non supportato. Solo file .json sono accettati.')
      onImportError?.(['Formato non supportato'])
      setTimeout(() => setState('idle'), 3000)
      return
    }

    setState('loading')

    try {
      // Read file content
      const content = await file.text()

      // Try to parse JSON first
      try {
        JSON.parse(content)
      } catch (parseError) {
        throw new Error('File JSON non valido')
      }

      // Initialize wizard from JSON
      const result = initFromJson(content)

      if (result.ok) {
        setState('success')
        onImportSuccess?.()
        setTimeout(() => setState('idle'), 2000)
      } else {
        setState('error')
        setErrorMessage(result.errors.join(', '))
        onImportError?.(result.errors)
        setTimeout(() => setState('idle'), 4000)
      }
    } catch (error) {
      setState('error')
      setErrorMessage(error instanceof Error ? error.message : 'Errore durante l\'importazione')
      onImportError?.([error instanceof Error ? error.message : 'Errore sconosciuto'])
      setTimeout(() => setState('idle'), 3000)
    }
  }

  const handleDrop = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    e.stopPropagation()
    setState('idle')

    const file = e.dataTransfer.files[0]
    if (file) {
      handleFileSelect(file)
    }
  }

  const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    e.stopPropagation()
    if (state === 'idle') {
      setState('hover')
    }
  }

  const handleDragLeave = (e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault()
    e.stopPropagation()
    if (state === 'hover') {
      setState('idle')
    }
  }

  const handleClick = () => {
    if (state === 'idle') {
      inputRef.current?.click()
    }
  }

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    if (file) {
      handleFileSelect(file)
    }
    // Reset input value to allow re-selecting the same file
    e.target.value = ''
  }

  return (
    <div className={className}>
      <div
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={handleClick}
        className={`
          relative
          border-2 border-dashed rounded-lg
          p-8 text-center
          transition-all duration-200
          ${getDropzoneStyles(state)}
        `}
        role="button"
        tabIndex={0}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            handleClick()
          }
        }}
      >
        {/* Hidden File Input */}
        <input
          ref={inputRef}
          type="file"
          accept=".json"
          onChange={handleInputChange}
          className="hidden"
          aria-label="Choose JSON file"
        />

        {/* Idle/Hover State */}
        {(state === 'idle' || state === 'hover') && (
          <div className="space-y-4">
            <div className={`inline-flex items-center justify-center w-16 h-16 rounded-full bg-[var(--wz-amber-dim)] text-[var(--wz-amber)] ${state === 'hover' ? 'wz-bounce' : ''}`}>
              <ArrowUpTrayIcon className="w-8 h-8" />
            </div>
            <div>
              <p className="text-lg font-medium text-[var(--wz-text)]">
                Trascina il file JSON qui
              </p>
              <p className="text-sm text-[var(--wz-muted)] mt-1">
                oppure clicca per scegliere
              </p>
            </div>
          </div>
        )}

        {/* Loading State */}
        {state === 'loading' && (
          <div className="space-y-4">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-[var(--wz-amber-dim)]">
              <div className="w-8 h-8 border-4 border-[var(--wz-amber)] border-t-transparent rounded-full animate-spin" />
            </div>
            <p className="text-lg font-medium text-[var(--wz-text)]">
              Importazione in corso...
            </p>
          </div>
        )}

        {/* Success State */}
        {state === 'success' && (
          <div className="space-y-4">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-[var(--wz-success)] bg-opacity-20">
              <CheckCircleIcon className="w-10 h-10 text-[var(--wz-success)]" />
            </div>
            <p className="text-lg font-medium text-[var(--wz-success)]">
              Importazione completata!
            </p>
          </div>
        )}

        {/* Error State */}
        {state === 'error' && (
          <div className="space-y-4">
            <div className="inline-flex items-center justify-center w-16 h-16 rounded-full bg-[var(--wz-error)] bg-opacity-20">
              <XCircleIcon className="w-10 h-10 text-[var(--wz-error)]" />
            </div>
            <div>
              <p className="text-lg font-medium text-[var(--wz-error)]">
                Errore durante l'importazione
              </p>
              {errorMessage && (
                <p className="text-sm text-[var(--wz-error)] mt-2 opacity-80">
                  {errorMessage}
                </p>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

// ============================================================================
// STYLE HELPERS
// ============================================================================

function getDropzoneStyles(state: DropzoneState): string {
  switch (state) {
    case 'idle':
      return 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)] cursor-pointer hover:bg-[var(--wz-amber-glow)]'

    case 'hover':
      return 'border-[var(--wz-amber)] bg-[var(--wz-amber-glow)] cursor-pointer scale-[1.02]'

    case 'loading':
      return 'border-[var(--wz-amber)] bg-[var(--wz-amber-dim)] cursor-wait'

    case 'success':
      return 'border-[var(--wz-success)] bg-[var(--wz-success)] bg-opacity-10 cursor-default'

    case 'error':
      return 'border-[var(--wz-error)] bg-[var(--wz-error)] bg-opacity-10 cursor-pointer'

    default:
      return ''
  }
}
