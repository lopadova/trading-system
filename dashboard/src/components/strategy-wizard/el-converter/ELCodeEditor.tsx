/**
 * ELCodeEditor — Code Editor with Syntax Highlighting
 *
 * Features:
 * - Syntax highlighting overlay for EasyLanguage
 * - Tab key inserts 4 spaces (no focus change)
 * - Resizable textarea
 * - Monospace font (JetBrains Mono)
 */

import { useRef, type KeyboardEvent } from 'react'
import { ELSyntaxHighlight } from './ELSyntaxHighlight'

interface ELCodeEditorProps {
  value: string
  onChange: (value: string) => void
  placeholder?: string
  minHeight?: number
}

export function ELCodeEditor({
  value,
  onChange,
  placeholder = 'Incolla qui il codice EasyLanguage...',
  minHeight = 300
}: ELCodeEditorProps) {
  const textareaRef = useRef<HTMLTextAreaElement>(null)

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    // Tab key → 4 spazi (non cambio focus)
    if (e.key === 'Tab') {
      e.preventDefault()
      const start = e.currentTarget.selectionStart
      const end = e.currentTarget.selectionEnd
      const newValue = value.substring(0, start) + '    ' + value.substring(end)
      onChange(newValue)

      // Ripristina cursore dopo i 4 spazi
      setTimeout(() => {
        if (textareaRef.current) {
          textareaRef.current.selectionStart = start + 4
          textareaRef.current.selectionEnd = start + 4
        }
      }, 0)
    }
  }

  return (
    <div className="relative font-mono text-sm">
      {/* Textarea con syntax highlighting overlay */}
      <div className="relative">
        {/* Background syntax highlighted */}
        <div
          className="absolute inset-0 pointer-events-none overflow-auto whitespace-pre-wrap break-words"
          style={{
            backgroundColor: '#0d1117',
            padding: '12px',
            minHeight: `${minHeight}px`
          }}
        >
          <ELSyntaxHighlight code={value || placeholder} />
        </div>

        {/* Textarea trasparente sopra */}
        <textarea
          ref={textareaRef}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          spellCheck={false}
          className="relative w-full resize-y bg-transparent text-transparent caret-white outline-none"
          style={{
            padding: '12px',
            minHeight: `${minHeight}px`,
            lineHeight: '1.5',
            fontFamily: 'JetBrains Mono, Monaco, Consolas, monospace'
          }}
        />
      </div>
    </div>
  )
}
