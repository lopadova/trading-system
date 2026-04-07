/**
 * ELConverterPanel — EasyLanguage to SDF Converter UI
 *
 * Features:
 * - Split-pane layout (editor left, result right)
 * - Paste example button
 * - Clear button
 * - Convert with AI button
 * - Result panel with issues, warnings, JSON preview
 * - Apply to wizard navigation
 * - Responsive: mobile → stack vertical
 */

import { useState } from 'react'
import { useNavigate } from '@tanstack/react-router'
import { ELCodeEditor } from './ELCodeEditor'
import { EL_EXAMPLES } from './el-examples'
import { Button } from '../../ui/Button'
import { ConversionResultPanel } from './ConversionResultPanel'
import { useELConversion } from '../../../hooks/useELConversion'
import { useWizardStore } from '../../../stores/wizardStore'

export function ELConverterPanel() {
  const [code, setCode] = useState('')
  const { convert, isLoading, result, error, reset } = useELConversion()
  const { applyConversionResult } = useWizardStore()
  const navigate = useNavigate()

  const handlePasteExample = () => {
    setCode(EL_EXAMPLES.ironCondor)
  }

  const handleClear = () => {
    setCode('')
    reset()
  }

  const handleConvert = async () => {
    if (!code.trim()) return

    try {
      await convert(code)
    } catch (err) {
      // Error already handled by useELConversion
      console.error('Conversion failed:', err)
    }
  }

  const handleApplyToWizard = () => {
    if (!result?.result_json) return

    // Apply to wizard store
    applyConversionResult(result.result_json)

    // Navigate to wizard step 1
    navigate({ to: '/strategies/wizard', search: { step: 1 } })
  }

  return (
    <div className="flex flex-col lg:flex-row gap-4 h-full">
      {/* Left: Editor */}
      <div className="flex-1 flex flex-col border border-gray-700 rounded-lg overflow-hidden">
        <div className="bg-gray-800 px-4 py-2 border-b border-gray-700 flex items-center justify-between">
          <h3 className="text-sm font-semibold text-gray-200">Codice EasyLanguage</h3>
          <div className="flex gap-2">
            <Button size="sm" variant="ghost" onClick={handlePasteExample}>
              📋 Incolla esempio
            </Button>
            <Button size="sm" variant="ghost" onClick={handleClear} disabled={!code}>
              🗑 Cancella
            </Button>
          </div>
        </div>

        <div className="flex-1 overflow-auto">
          <ELCodeEditor value={code} onChange={setCode} minHeight={400} />
        </div>
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

      {/* Bottom: Convert button (visible on mobile) */}
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
