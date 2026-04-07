/**
 * JSONPreview — Syntax-Highlighted JSON Viewer with Actions
 *
 * Features:
 * - Syntax highlighting with prism-react-renderer
 * - Copy to clipboard button
 * - Download as JSON file button
 * - Line numbers
 * - Max height with scroll
 */

import { useState } from 'react'
import { Highlight, themes } from 'prism-react-renderer'
import { ClipboardDocumentIcon, CheckIcon, ArrowDownTrayIcon } from '@heroicons/react/24/outline'
import '../../../styles/wizard.css'

// ============================================================================
// TYPES
// ============================================================================

export interface JSONPreviewProps {
  data: unknown
  filename?: string
  maxHeight?: string
  className?: string
}

// ============================================================================
// COMPONENT
// ============================================================================

export function JSONPreview({
  data,
  filename = 'strategy.json',
  maxHeight = '500px',
  className = '',
}: JSONPreviewProps) {
  const [copied, setCopied] = useState(false)

  const jsonString = JSON.stringify(data, null, 2)

  const handleCopy = async () => {
    try {
      await navigator.clipboard.writeText(jsonString)
      setCopied(true)
      setTimeout(() => setCopied(false), 2000)
    } catch (error) {
      console.error('Failed to copy to clipboard:', error)
    }
  }

  const handleDownload = () => {
    try {
      const blob = new Blob([jsonString], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const link = document.createElement('a')
      link.href = url
      link.download = filename
      document.body.appendChild(link)
      link.click()
      document.body.removeChild(link)
      URL.revokeObjectURL(url)
    } catch (error) {
      console.error('Failed to download file:', error)
    }
  }

  return (
    <div className={`json-preview ${className}`}>
      {/* Action Bar */}
      <div className="flex items-center justify-between px-4 py-2 bg-[var(--wz-surface)] border border-[var(--wz-border)] border-b-0 rounded-t-lg">
        <div className="flex items-center gap-2">
          <div className="w-3 h-3 rounded-full bg-[var(--wz-error)]" />
          <div className="w-3 h-3 rounded-full bg-[var(--wz-warning)]" />
          <div className="w-3 h-3 rounded-full bg-[var(--wz-success)]" />
          <span className="ml-3 text-xs font-mono text-[var(--wz-muted)]">{filename}</span>
        </div>

        <div className="flex items-center gap-2">
          {/* Copy Button */}
          <button
            type="button"
            onClick={handleCopy}
            className="
              inline-flex items-center gap-1.5 px-3 py-1.5 rounded
              text-xs font-medium
              text-[var(--wz-muted)] hover:text-[var(--wz-text)]
              hover:bg-[var(--wz-elevated)]
              transition-colors
            "
            title="Copy to clipboard"
          >
            {copied ? (
              <>
                <CheckIcon className="w-4 h-4 text-[var(--wz-success)]" />
                <span className="text-[var(--wz-success)]">Copiato!</span>
              </>
            ) : (
              <>
                <ClipboardDocumentIcon className="w-4 h-4" />
                <span>Copia</span>
              </>
            )}
          </button>

          {/* Download Button */}
          <button
            type="button"
            onClick={handleDownload}
            className="
              inline-flex items-center gap-1.5 px-3 py-1.5 rounded
              text-xs font-medium
              text-[var(--wz-muted)] hover:text-[var(--wz-text)]
              hover:bg-[var(--wz-elevated)]
              transition-colors
            "
            title="Download JSON file"
          >
            <ArrowDownTrayIcon className="w-4 h-4" />
            <span>Download</span>
          </button>
        </div>
      </div>

      {/* Code Block */}
      <div
        className="overflow-y-auto border border-[var(--wz-border)] rounded-b-lg"
        style={{ maxHeight }}
      >
        <Highlight theme={themes.nightOwl} code={jsonString} language="json">
          {({ className: highlightClassName, style, tokens, getLineProps, getTokenProps }) => (
            <pre
              className={`${highlightClassName} p-4 text-sm font-mono bg-[var(--wz-bg)] m-0`}
              style={style}
            >
              <code>
                {tokens.map((line, i) => (
                  <div key={i} {...getLineProps({ line })} className="table-row">
                    {/* Line Number */}
                    <span className="table-cell pr-4 text-right select-none text-[var(--wz-muted)] opacity-50">
                      {i + 1}
                    </span>

                    {/* Line Content */}
                    <span className="table-cell">
                      {line.map((token, key) => (
                        <span key={key} {...getTokenProps({ token })} />
                      ))}
                    </span>
                  </div>
                ))}
              </code>
            </pre>
          )}
        </Highlight>
      </div>
    </div>
  )
}
