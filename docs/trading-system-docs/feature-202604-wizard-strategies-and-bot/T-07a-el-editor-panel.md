# T-SW-07a — EasyLanguage Editor Panel (Frontend Only)

## Obiettivo
Implementare il pannello di editing EasyLanguage con syntax highlighting base,
gestione Tab key, bottoni di utilità, e layout responsive split-pane.
Nessuna integrazione API in questo task (solo UI).

## Dipendenze
- T-SW-02 (wizard store per applyConversionResult placeholder)
- T-SW-03 (componenti UI base)

## Files da Creare
- `dashboard/src/components/strategy-wizard/el-converter/ELCodeEditor.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/ELConverterPanel.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/ELSyntaxHighlight.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/el-examples.ts`

## Files da Modificare
Nessuno.

## Implementazione

### ELCodeEditor.tsx — Editor styled con highlighting

```tsx
import { useState, useRef, KeyboardEvent } from 'react'
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
```

### ELSyntaxHighlight.tsx — Highlighting base EasyLanguage

```tsx
interface Token {
  type: 'keyword' | 'string' | 'number' | 'comment' | 'operator' | 'text'
  value: string
}

/**
 * Tokenizza codice EasyLanguage per syntax highlighting.
 * Regole base:
 * - Keywords: inputs, variables, begin, end, if, then, else, buy, sell
 * - Strings: "..." or '...'
 * - Numbers: 0-9 con decimali
 * - Comments: // line comment
 */
function tokenizeEasyLanguage(code: string): Token[] {
  const tokens: Token[] = []
  const keywords = new Set([
    'inputs', 'variables', 'begin', 'end', 'if', 'then', 'else',
    'buy', 'sell', 'sellshort', 'buytocover', 'for', 'to', 'downto',
    'while', 'do', 'true', 'false', 'and', 'or', 'not',
    'value', 'var', 'array', 'of', 'plot', 'print'
  ])

  const lines = code.split('\n')
  
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i]
    let j = 0

    while (j < line.length) {
      // Skip whitespace
      if (/\s/.test(line[j])) {
        tokens.push({ type: 'text', value: line[j] })
        j++
        continue
      }

      // Comment: //
      if (line.substring(j, j + 2) === '//') {
        tokens.push({ type: 'comment', value: line.substring(j) })
        break
      }

      // String: "..." or '...'
      if (line[j] === '"' || line[j] === "'") {
        const quote = line[j]
        let endIndex = j + 1
        while (endIndex < line.length && line[endIndex] !== quote) {
          endIndex++
        }
        tokens.push({ type: 'string', value: line.substring(j, endIndex + 1) })
        j = endIndex + 1
        continue
      }

      // Number: 0-9
      if (/\d/.test(line[j])) {
        let endIndex = j
        while (endIndex < line.length && /[\d.]/.test(line[endIndex])) {
          endIndex++
        }
        tokens.push({ type: 'number', value: line.substring(j, endIndex) })
        j = endIndex
        continue
      }

      // Keyword or identifier
      if (/[a-zA-Z_]/.test(line[j])) {
        let endIndex = j
        while (endIndex < line.length && /[a-zA-Z0-9_]/.test(line[endIndex])) {
          endIndex++
        }
        const word = line.substring(j, endIndex).toLowerCase()
        const type = keywords.has(word) ? 'keyword' : 'text'
        tokens.push({ type, value: line.substring(j, endIndex) })
        j = endIndex
        continue
      }

      // Operator: + - * / = < > ( ) [ ] { } , ;
      if (/[+\-*\/=<>()[\]{},;]/.test(line[j])) {
        tokens.push({ type: 'operator', value: line[j] })
        j++
        continue
      }

      // Altro
      tokens.push({ type: 'text', value: line[j] })
      j++
    }

    // Newline
    if (i < lines.length - 1) {
      tokens.push({ type: 'text', value: '\n' })
    }
  }

  return tokens
}

export function ELSyntaxHighlight({ code }: { code: string }) {
  const tokens = tokenizeEasyLanguage(code)

  const colorMap: Record<Token['type'], string> = {
    keyword: '#fbbf24',  // amber
    string: '#34d399',   // green
    number: '#60a5fa',   // blue
    comment: '#6b7280',  // gray
    operator: '#f472b6', // pink
    text: '#e5e7eb'      // light gray
  }

  return (
    <>
      {tokens.map((token, i) => (
        <span key={i} style={{ color: colorMap[token.type] }}>
          {token.value}
        </span>
      ))}
    </>
  )
}
```

### ELConverterPanel.tsx — Layout split-pane

```tsx
import { useState } from 'react'
import { ELCodeEditor } from './ELCodeEditor'
import { EL_EXAMPLES } from './el-examples'
import { Button } from '../../ui/Button'

export function ELConverterPanel() {
  const [code, setCode] = useState('')
  const [isConverting, setIsConverting] = useState(false)

  const handlePasteExample = () => {
    setCode(EL_EXAMPLES.ironCondor)
  }

  const handleClear = () => {
    setCode('')
  }

  const handleConvert = async () => {
    setIsConverting(true)
    // TODO T-07b: chiamata API
    setTimeout(() => setIsConverting(false), 2000)
  }

  return (
    <div className="flex flex-col lg:flex-row gap-4 h-full">
      {/* Left: Editor */}
      <div className="flex-1 flex flex-col border border-gray-700 rounded-lg overflow-hidden">
        <div className="bg-gray-800 px-4 py-2 border-b border-gray-700 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-200">Codice EasyLanguage</h3>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="ghost"
              onClick={handlePasteExample}
            >
              📋 Incolla esempio
            </Button>
            <Button
              size="sm"
              variant="ghost"
              onClick={handleClear}
              disabled={!code}
            >
              🗑 Cancella
            </Button>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          <ELCodeEditor
            value={code}
            onChange={setCode}
            minHeight={400}
          />
        </div>
      </div>

      {/* Right: Result (placeholder per T-07c) */}
      <div className="flex-1 flex flex-col border border-gray-700 rounded-lg overflow-hidden">
        <div className="bg-gray-800 px-4 py-2 border-b border-gray-700">
          <h3 className="text-sm font-semibold text-gray-200">Risultato Conversione</h3>
        </div>

        <div className="flex-1 flex items-center justify-center p-8 text-center text-gray-400">
          {isConverting ? (
            <div className="animate-pulse">🤖 Claude sta analizzando...</div>
          ) : (
            <div>Incolla codice EasyLanguage e premi Converti</div>
          )}
        </div>
      </div>

      {/* Bottom: Convert button */}
      <div className="lg:hidden mt-4">
        <Button
          className="w-full"
          onClick={handleConvert}
          disabled={!code.trim() || isConverting}
        >
          🤖 Converti con AI →
        </Button>
      </div>
    </div>
  )
}
```

### el-examples.ts — Esempi EasyLanguage

```typescript
export const EL_EXAMPLES = {
  ironCondor: `inputs:
  ShortStrike(30),
  LongStrike(20),
  ProfitTarget(500),
  StopLoss(1000);

variables:
  DaysToExp(45),
  Delta(0.30);

if DaysToExp = 45 then begin
  // Sell put spread
  SellShort next bar at market;
  
  // Buy protective put
  BuyTocover next bar at market;
end;

if NetProfit >= ProfitTarget or NetProfit <= -StopLoss then begin
  // Close all positions
  ExitLong next bar at market;
  ExitShort next bar at market;
end;`,

  simplePutSell: `inputs:
  TargetDelta(0.30),
  DTE(45),
  ProfitTarget(300);

if DaysToExpiration = DTE then
  Sell next bar at market;

if NetProfit >= ProfitTarget then
  ExitShort next bar at market;`
}
```

## Test

- `TEST-SW-07a-01`: ELCodeEditor Tab key → 4 spazi inseriti (no blur)
- `TEST-SW-07a-02`: Tab con selezione testo → 4 spazi sostituiscono selezione
- `TEST-SW-07a-03`: Syntax highlight keywords → colore amber
- `TEST-SW-07a-04`: Syntax highlight strings → colore verde
- `TEST-SW-07a-05`: Syntax highlight numbers → colore blu
- `TEST-SW-07a-06`: Syntax highlight comments // → colore grigio
- `TEST-SW-07a-07`: "Incolla esempio" button → editor precompilato con ironCondor
- `TEST-SW-07a-08`: "Cancella" button → editor vuoto
- `TEST-SW-07a-09`: "Converti" button disabled se code vuoto
- `TEST-SW-07a-10`: Layout responsive: mobile → stack verticale
- `TEST-SW-07a-11`: Textarea resize-y funziona correttamente
- `TEST-SW-07a-12`: JetBrains Mono font applicato correttamente

## Done Criteria

- [ ] `npm run build` compila senza errori
- [ ] Tutti i test TEST-SW-07a-XX passano
- [ ] No regression su test esistenti
- [ ] Syntax highlighting visibile e corretto su esempio ironCondor
- [ ] Tab key testato manualmente (non cambia focus)
- [ ] Layout responsive testato su mobile/tablet/desktop

## Stima

~6 ore
