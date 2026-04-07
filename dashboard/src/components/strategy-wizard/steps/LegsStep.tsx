/**
 * LegsStep Component
 *
 * Wizard Step 5: Legs Management
 *
 * Allows users to define the strategy structure by adding, editing, and removing:
 * - Primary legs (income/directional legs)
 * - Protection legs (hedges)
 *
 * Uses wizardStore for state management and validation.
 */

import { useState } from 'react'
import { useWizardStore } from '../../../stores/wizardStore'
import { createDefaultLeg } from '../../../utils/sdf-defaults'
import { isStrategyLeg } from '../../../types/sdf-v1'
import { LegCard } from './LegCard'
import { LegEditor } from './LegEditor'
import { Button } from '../../ui/Button'
import { Card } from '../../ui/Card'

export function LegsStep() {
  const { draft, setField } = useWizardStore()

  // Get legs and protection_legs from draft
  const legs = draft.structure?.legs ?? []
  const protectionLegs = draft.structure?.protection_legs ?? []

  // Track which leg is being edited
  const [editingLegId, setEditingLegId] = useState<string | null>(null)
  const [editingType, setEditingType] = useState<'legs' | 'protection_legs'>('legs')

  /**
   * Add a new leg to the specified array (legs or protection_legs)
   */
  const handleAddLeg = (type: 'legs' | 'protection_legs') => {
    const currentLegs = type === 'legs' ? legs : protectionLegs
    const index = currentLegs.length

    // Income legs default to sell, protection legs default to buy
    const action = type === 'legs' ? 'sell' : 'buy'
    const newLeg = createDefaultLeg(index, action)

    // Add to array
    setField(`structure.${type}`, [...currentLegs, newLeg])

    // Open editor for new leg
    setEditingLegId(newLeg.leg_id)
    setEditingType(type)
  }

  /**
   * Remove a leg from the specified array
   */
  const handleRemoveLeg = (legId: string, type: 'legs' | 'protection_legs') => {
    const currentLegs = type === 'legs' ? legs : protectionLegs
    const updated = currentLegs.filter((l) => l?.leg_id !== legId)
    setField(`structure.${type}`, updated)

    // Close editor if this leg was being edited
    if (editingLegId === legId) {
      setEditingLegId(null)
    }
  }

  /**
   * Open editor for a specific leg
   */
  const handleEditLeg = (legId: string, type: 'legs' | 'protection_legs') => {
    setEditingLegId(legId)
    setEditingType(type)
  }

  /**
   * Close editor (save changes)
   */
  const handleSaveLeg = () => {
    setEditingLegId(null)
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h2 className="text-2xl font-bold mb-2">Struttura Legs</h2>
        <p className="text-muted">
          Definisci i legs della strategia: income/directional legs e protection legs (opzionali)
        </p>
      </div>

      {/* Primary Legs Section */}
      <Card className="p-6">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold">Primary Legs (Income/Directional)</h3>
          <Button onClick={() => handleAddLeg('legs')}>+ Aggiungi Leg</Button>
        </div>

        {legs.length === 0 ? (
          <p className="text-muted text-center py-8">
            Nessun leg configurato. Clicca "+ Aggiungi Leg" per iniziare.
          </p>
        ) : (
          <div className="space-y-3">
            {legs.map((leg) => {
              // Type guard: only render valid StrategyLeg objects
              if (!leg || !isStrategyLeg(leg)) return null

              return editingLegId === leg.leg_id && editingType === 'legs' ? (
                <LegEditor
                  key={leg.leg_id}
                  leg={leg}
                  type="legs"
                  onSave={handleSaveLeg}
                  onCancel={() => setEditingLegId(null)}
                />
              ) : (
                <LegCard
                  key={leg.leg_id}
                  leg={leg}
                  onEdit={() => handleEditLeg(leg.leg_id, 'legs')}
                  onRemove={() => handleRemoveLeg(leg.leg_id, 'legs')}
                />
              )
            })}
          </div>
        )}
      </Card>

      {/* Protection Legs Section */}
      <Card className="p-6">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold">Protection Legs (Hedge)</h3>
          <Button onClick={() => handleAddLeg('protection_legs')} variant="secondary">
            + Aggiungi Protection
          </Button>
        </div>

        {protectionLegs.length === 0 ? (
          <p className="text-muted text-center py-8 text-sm">
            Nessuna protezione configurata (opzionale)
          </p>
        ) : (
          <div className="space-y-3">
            {protectionLegs.map((leg) => {
              // Type guard: only render valid StrategyLeg objects
              if (!leg || !isStrategyLeg(leg)) return null

              return editingLegId === leg.leg_id && editingType === 'protection_legs' ? (
                <LegEditor
                  key={leg.leg_id}
                  leg={leg}
                  type="protection_legs"
                  onSave={handleSaveLeg}
                  onCancel={() => setEditingLegId(null)}
                />
              ) : (
                <LegCard
                  key={leg.leg_id}
                  leg={leg}
                  onEdit={() => handleEditLeg(leg.leg_id, 'protection_legs')}
                  onRemove={() => handleRemoveLeg(leg.leg_id, 'protection_legs')}
                />
              )
            })}
          </div>
        )}
      </Card>

      {/* Summary */}
      {(legs.length > 0 || protectionLegs.length > 0) && (
        <Card className="p-4 bg-blue-500/10 border-blue-500/20">
          <p className="text-sm">
            <strong>Riepilogo:</strong> {legs.length} primary leg
            {legs.length !== 1 ? 's' : ''}, {protectionLegs.length} protection leg
            {protectionLegs.length !== 1 ? 's' : ''}
          </p>
        </Card>
      )}
    </div>
  )
}
