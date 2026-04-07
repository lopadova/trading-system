# T-SW-07c — Conversion Result Panel + Apply to Wizard

## Obiettivo
Implementare pannello visualizzazione risultati conversione EasyLanguage con accordion
(issues/preview/notes), hook per chiamata API, e bottone "Applica al Wizard" che
precompila il draft e naviga allo step 1.

## Dipendenze
- T-SW-07a (editor panel)
- T-SW-07b (worker endpoint)
- T-SW-02 (wizard store con applyConversionResult method)

## Files da Creare
- `dashboard/src/components/strategy-wizard/el-converter/ConversionResultPanel.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/IssuesList.tsx`
- `dashboard/src/components/strategy-wizard/el-converter/JSONPreview.tsx`
- `dashboard/src/hooks/useELConversion.ts`

## Files da Modificare
- `dashboard/src/components/strategy-wizard/el-converter/ELConverterPanel.tsx` — integrare result panel e hook

## Implementazione

### useELConversion.ts — React hook per API call

```typescript
import { useState } from 'react'
import type { StrategyDefinition } from '../types/sdf-v1'

export interface ConversionIssue {
  type: 'not_supported' | 'ambiguous' | 'manual_required'
  el_construct: string
  description: string
  suggestion: string
}

export interface ConversionResult {
  convertible: boolean | 'partial'
  confidence: number
  result_json: Partial<StrategyDefinition> | null
  issues: ConversionIssue[]
  warnings: string[]
  notes: string
}

export interface ConversionState {
  isLoading: boolean
  result: ConversionResult | null
  error: string | null
}

export function useELConversion() {
  const [state, setState] = useState<ConversionState>({
    isLoading: false,
    result: null,
    error: null
  })

  const convert = async (easylanguageCode: string) => {
    setState({ isLoading: true, result: null, error: null })

    try {
      const response = await fetch('/api/v1/strategies/convert-el', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'X-Api-Key': import.meta.env.VITE_WORKER_API_KEY || ''
        },
        body: JSON.stringify({ easylanguage_code: easylanguageCode })
      })

      if (!response.ok) {
        const errorData = await response.json().catch(() => ({}))
        
        if (response.status === 503) {
          throw new Error('Conversione AI non disponibile: API key non configurata')
        }
        
        throw new Error(errorData.message || `HTTP ${response.status}`)
      }

      const result: ConversionResult = await response.json()

      setState({ isLoading: false, result, error: null })
      return result

    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Errore sconosciuto'
      setState({ isLoading: false, result: null, error: errorMessage })
      throw err
    }
  }

  const reset = () => {
    setState({ isLoading: false, result: null, error: null })
  }

  return {
    ...state,
    convert,
    reset
  }
}
```

### ConversionResultPanel.tsx — Result visualization

```tsx
import { ConversionResult } from '../../../hooks/useELConversion'
import { IssuesList } from './IssuesList'
import { JSONPreview } from './JSONPreview'
import { Button } from '../../ui/Button'
import { Badge } from '../../ui/Badge'

interface ConversionResultPanelProps {
  result: ConversionResult | null
  isLoading: boolean
  error: string | null
  onApplyToWizard: () => void
}

export function ConversionResultPanel({
  result,
  isLoading,
  error,
  onApplyToWizard
}: ConversionResultPanelProps) {
  if (isLoading) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <div className="animate-pulse text-6xl mb-4">🤖</div>
        <p className="text-lg font-medium text-gray-200">Claude sta analizzando...</p>
        <p className="text-sm text-gray-400 mt-2">Questo può richiedere fino a 30 secondi</p>
      </div>
    )
  }

  if (error) {
    return (
      <div className="flex flex-col items-center justify-center h-full p-8 text-center">
        <div className="text-6xl mb-4">❌</div>
        <p className="text-lg font-medium text-red-400 mb-2">Errore durante conversione</p>
        <p className="text-sm text-gray-400">{error}</p>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="flex items-center justify-center h-full p-8 text-center text-gray-400">
        <div>
          <p className="text-lg mb-2">Nessun risultato</p>
          <p className="text-sm">Incolla codice EasyLanguage e premi Converti</p>
        </div>
      </div>
    )
  }

  // Determina stato conversione
  const isFullyConvertible = result.convertible === true
  const isPartiallyConvertible = result.convertible === 'partial'
  const isNotConvertible = result.convertible === false

  return (
    <div className="flex flex-col h-full">
      {/* Header con badge */}
      <div className="p-4 border-b border-gray-700 flex items-center justify-between">
        <div className="flex items-center gap-3">
          {isFullyConvertible && (
            <>
              <div className="text-3xl">✅</div>
              <div>
                <Badge variant="success">Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  Confidence: {(result.confidence * 100).toFixed(0)}%
                </p>
              </div>
            </>
          )}

          {isPartiallyConvertible && (
            <>
              <div className="text-3xl">⚠️</div>
              <div>
                <Badge variant="warning">Parzialmente Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  Confidence: {(result.confidence * 100).toFixed(0)}%
                </p>
              </div>
            </>
          )}

          {isNotConvertible && (
            <>
              <div className="text-3xl">❌</div>
              <div>
                <Badge variant="error">Non Convertibile</Badge>
                <p className="text-xs text-gray-400 mt-1">
                  {result.issues.length} issues trovati
                </p>
              </div>
            </>
          )}
        </div>

        {/* Apply button (solo se convertibile) */}
        {(isFullyConvertible || isPartiallyConvertible) && result.result_json && (
          <Button onClick={onApplyToWizard}>
            🔧 Applica al Wizard →
          </Button>
        )}
      </div>

      {/* Accordion sections */}
      <div className="flex-1 overflow-auto p-4 space-y-4">
        {/* Issues */}
        {result.issues.length > 0 && (
          <details open={result.issues.length > 0}>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Issues ({result.issues.length})
            </summary>
            <IssuesList issues={result.issues} />
          </details>
        )}

        {/* Warnings */}
        {result.warnings.length > 0 && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Avvisi ({result.warnings.length})
            </summary>
            <ul className="space-y-2">
              {result.warnings.map((warning, i) => (
                <li key={i} className="text-sm text-yellow-400 bg-yellow-400/10 p-2 rounded">
                  {warning}
                </li>
              ))}
            </ul>
          </details>
        )}

        {/* JSON Preview */}
        {result.result_json && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Preview JSON
            </summary>
            <JSONPreview json={result.result_json} />
          </details>
        )}

        {/* Notes */}
        {result.notes && (
          <details>
            <summary className="cursor-pointer font-semibold text-gray-200 mb-2">
              Note
            </summary>
            <p className="text-sm text-gray-400 bg-gray-800 p-3 rounded whitespace-pre-wrap">
              {result.notes}
            </p>
          </details>
        )}
      </div>
    </div>
  )
}
```

### IssuesList.tsx — Issues display

```tsx
import type { ConversionIssue } from '../../../hooks/useELConversion'
import { Badge } from '../../ui/Badge'

interface IssuesListProps {
  issues: ConversionIssue[]
}

export function IssuesList({ issues }: IssuesListProps) {
  const getIssueIcon = (type: ConversionIssue['type']) => {
    switch (type) {
      case 'not_supported': return '🔴'
      case 'ambiguous': return '🟡'
      case 'manual_required': return '🟠'
    }
  }

  const getIssueLabel = (type: ConversionIssue['type']) => {
    switch (type) {
      case 'not_supported': return 'Non Supportato'
      case 'ambiguous': return 'Ambiguo'
      case 'manual_required': return 'Richiesta Modifica Manuale'
    }
  }

  return (
    <div className="space-y-3">
      {issues.map((issue, i) => (
        <div key={i} className="border border-gray-700 rounded-lg p-3 bg-gray-800">
          <div className="flex items-start gap-2 mb-2">
            <span className="text-xl">{getIssueIcon(issue.type)}</span>
            <div className="flex-1">
              <div className="flex items-center gap-2 mb-1">
                <Badge variant={issue.type === 'not_supported' ? 'error' : 'warning'}>
                  {getIssueLabel(issue.type)}
                </Badge>
                <code className="text-xs text-amber-400 bg-amber-400/10 px-2 py-0.5 rounded">
                  {issue.el_construct}
                </code>
              </div>
              <p className="text-sm text-gray-300 mb-2">{issue.description}</p>
              {issue.suggestion && (
                <div className="text-xs text-blue-400 bg-blue-400/10 p-2 rounded">
                  💡 <strong>Suggerimento:</strong> {issue.suggestion}
                </div>
              )}
            </div>
          </div>
        </div>
      ))}
    </div>
  )
}
```

### JSONPreview.tsx — Syntax highlighted JSON

```tsx
export function JSONPreview({ json }: { json: unknown }) {
  const jsonString = JSON.stringify(json, null, 2)

  return (
    <pre className="text-xs font-mono bg-gray-900 p-3 rounded overflow-auto max-h-96 border border-gray-700">
      <code className="text-gray-300">{jsonString}</code>
    </pre>
  )
}
```

### ELConverterPanel.tsx — Update con integration

```tsx
// ... imports esistenti
import { ConversionResultPanel } from './ConversionResultPanel'
import { useELConversion } from '../../../hooks/useELConversion'
import { useWizardStore } from '../../../stores/wizardStore'
import { useNavigate } from 'react-router-dom'

export function ELConverterPanel() {
  const [code, setCode] = useState('')
  const { convert, isLoading, result, error, reset } = useELConversion()
  const { applyConversionResult } = useWizardStore()
  const navigate = useNavigate()

  const handleConvert = async () => {
    if (!code.trim()) return
    
    try {
      await convert(code)
    } catch (err) {
      // Errore già gestito da useELConversion
      console.error('Conversion failed:', err)
    }
  }

  const handleApplyToWizard = () => {
    if (!result?.result_json) return

    // Applica al wizard store
    applyConversionResult(result.result_json)

    // Naviga allo step 1
    navigate('/strategies/wizard?step=1')
  }

  const handleClear = () => {
    setCode('')
    reset()
  }

  return (
    <div className="flex flex-col lg:flex-row gap-4 h-full">
      {/* Left: Editor */}
      <div className="flex-1 flex flex-col border border-gray-700 rounded-lg overflow-hidden">
        {/* ... editor esistente ... */}
      </div>

      {/* Right: Result */}
      <div className="flex-1 flex flex-col border border-gray-700 rounded-lg overflow-hidden">
        <div className="bg-gray-800 px-4 py-2 border-b border-gray-700">
          <h3 className="text-sm font-semibold text-gray-200">Risultato Conversione</h3>
        </div>

        <ConversionResultPanel
          result={result}
          isLoading={isLoading}
          error={error}
          onApplyToWizard={handleApplyToWizard}
        />
      </div>

      {/* Bottom: Convert button */}
      <div className="lg:hidden mt-4">
        <Button
          className="w-full"
          onClick={handleConvert}
          disabled={!code.trim() || isLoading}
        >
          🤖 Converti con AI →
        </Button>
      </div>
    </div>
  )
}
```

## Test

- `TEST-SW-07c-01`: Conversione completa → badge "Convertibile" + bottone "Applica"
- `TEST-SW-07c-02`: Conversione parziale → badge "Parzialmente Convertibile" + bottone "Applica"
- `TEST-SW-07c-03`: Conversione fallita → badge "Non Convertibile" + NO bottone "Applica"
- `TEST-SW-07c-04`: `applyConversionResult()` chiamato con result_json
- `TEST-SW-07c-05`: Dopo apply → navigate to `/strategies/wizard?step=1`
- `TEST-SW-07c-06`: Issues accordion aperto di default se issues > 0
- `TEST-SW-07c-07`: JSON preview mostra syntax highlighting
- `TEST-SW-07c-08`: Loading state → "Claude sta analizzando..." con spinner
- `TEST-SW-07c-09`: Error state → messaggio errore chiaro
- `TEST-SW-07c-10`: Issue type "not_supported" → badge rosso + icona 🔴
- `TEST-SW-07c-11`: Issue type "ambiguous" → badge giallo + icona 🟡
- `TEST-SW-07c-12`: Issue suggestion presente → box blu con 💡

## Done Criteria

- [ ] `npm run build` compila senza errori
- [ ] Tutti i test TEST-SW-07c-XX passano
- [ ] No regression su test esistenti
- [ ] Test manuale conversione → apply → wizard precompilato
- [ ] Graceful degradation se API non disponibile
- [ ] Issue suggestions sempre mostrati quando presenti

## Stima

~6 ore
