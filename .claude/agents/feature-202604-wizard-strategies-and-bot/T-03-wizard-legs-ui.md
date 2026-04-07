# T-03 — Wizard Legs UI Component

## Obiettivo
Implementare il componente UI per la gestione dei legs della strategia (Step 5 del wizard).
Questo componente permette di aggiungere, modificare, rimuovere e riordinare i legs
(sia legs primari che protection_legs).

## Dipendenze
- T-00 (setup)
- T-01a (SDF types)
- T-01b (SDF validator)
- T-01c (SDF defaults)
- T-02 (wizard store)

## Files da Creare
- `dashboard/src/components/strategy-wizard/steps/LegsStep.tsx`
- `dashboard/src/components/strategy-wizard/steps/LegEditor.tsx`
- `dashboard/src/components/strategy-wizard/steps/LegCard.tsx`
- `dashboard/src/components/strategy-wizard/steps/LegsStep.test.tsx`

## Files da Modificare
- `dashboard/src/pages/StrategyWizardPage.tsx` — integrare LegsStep

## Implementazione

### LegsStep.tsx — Container principale

```typescript
/**
 * Step 5: Legs Management
 * 
 * Allows users to:
 * - Add primary legs (income/directional)
 * - Add protection legs (hedges)
 * - Edit leg parameters (DTE, delta, quantity, etc.)
 * - Reorder legs via drag & drop
 * - Remove legs
 * - Preview leg structure
 */
import { useWizardStore } from '../../../stores/wizardStore'
import { createDefaultLeg } from '../../../utils/sdf-defaults'
import { LegCard } from './LegCard'
import { LegEditor } from './LegEditor'
import { Button } from '../../ui/Button'
import { Card } from '../../ui/Card'

export function LegsStep() {
  const { draft, setField } = useWizardStore()
  const legs = draft.structure?.legs ?? []
  const protectionLegs = draft.structure?.protection_legs ?? []
  
  const [editingLegId, setEditingLegId] = useState<string | null>(null)
  const [editingType, setEditingType] = useState<'legs' | 'protection_legs'>('legs')

  const handleAddLeg = (type: 'legs' | 'protection_legs') => {
    const currentLegs = type === 'legs' ? legs : protectionLegs
    const index = currentLegs.length
    const action = type === 'legs' ? 'sell' : 'buy' // Income legs = sell, protection = buy
    const newLeg = createDefaultLeg(index, action)
    
    setField(`structure.${type}`, [...currentLegs, newLeg])
    setEditingLegId(newLeg.leg_id)
    setEditingType(type)
  }

  const handleRemoveLeg = (legId: string, type: 'legs' | 'protection_legs') => {
    const currentLegs = type === 'legs' ? legs : protectionLegs
    const updated = currentLegs.filter(l => l.leg_id !== legId)
    setField(`structure.${type}`, updated)
  }

  const handleEditLeg = (legId: string, type: 'legs' | 'protection_legs') => {
    setEditingLegId(legId)
    setEditingType(type)
  }

  const handleSaveLeg = () => {
    setEditingLegId(null)
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-bold mb-2">Struttura Legs</h2>
        <p className="text-muted">
          Definisci i legs della strategia: income/directional legs e protection legs (opzionali)
        </p>
      </div>

      {/* Primary Legs */}
      <Card className="p-6">
        <div className="flex items-center justify-between mb-4">
          <h3 className="text-lg font-semibold">Primary Legs (Income/Directional)</h3>
          <Button onClick={() => handleAddLeg('legs')}>
            + Aggiungi Leg
          </Button>
        </div>

        {legs.length === 0 ? (
          <p className="text-muted text-center py-8">
            Nessun leg configurato. Clicca "+ Aggiungi Leg" per iniziare.
          </p>
        ) : (
          <div className="space-y-3">
            {legs.map((leg) => (
              editingLegId === leg.leg_id && editingType === 'legs' ? (
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
            ))}
          </div>
        )}
      </Card>

      {/* Protection Legs */}
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
            {protectionLegs.map((leg) => (
              editingLegId === leg.leg_id && editingType === 'protection_legs' ? (
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
            ))}
          </div>
        )}
      </Card>

      {/* Summary */}
      {(legs.length > 0 || protectionLegs.length > 0) && (
        <Card className="p-4 bg-blue-500/10 border-blue-500/20">
          <p className="text-sm">
            <strong>Riepilogo:</strong> {legs.length} primary leg{legs.length !== 1 ? 's' : ''}, 
            {' '}{protectionLegs.length} protection leg{protectionLegs.length !== 1 ? 's' : ''}
          </p>
        </Card>
      )}
    </div>
  )
}
```

### LegCard.tsx — Leg display card (read-only)

```typescript
import type { StrategyLeg } from '../../../types/sdf-v1'
import { Card } from '../../ui/Card'
import { Button } from '../../ui/Button'
import { Badge } from '../../ui/Badge'

interface LegCardProps {
  leg: StrategyLeg
  onEdit: () => void
  onRemove: () => void
}

export function LegCard({ leg, onEdit, onRemove }: LegCardProps) {
  const actionColor = leg.action === 'buy' ? 'green' : 'red'
  const rightColor = leg.right === 'put' ? 'orange' : 'blue'

  return (
    <Card className="p-4">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          <div className="flex items-center gap-2 mb-2">
            <Badge color={actionColor}>{leg.action.toUpperCase()}</Badge>
            <Badge color={rightColor}>{leg.right.toUpperCase()}</Badge>
            <span className="font-mono text-sm text-muted">{leg.leg_id}</span>
          </div>
          
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            <div>
              <div className="text-muted text-xs">Target Delta</div>
              <div className="font-semibold">{leg.target_delta} ± {leg.delta_tolerance}</div>
            </div>
            <div>
              <div className="text-muted text-xs">Target DTE</div>
              <div className="font-semibold">{leg.target_dte} ± {leg.dte_tolerance}</div>
            </div>
            <div>
              <div className="text-muted text-xs">Quantity</div>
              <div className="font-semibold">{leg.quantity}</div>
            </div>
            <div>
              <div className="text-muted text-xs">Role</div>
              <div className="font-semibold capitalize">{leg.role}</div>
            </div>
          </div>

          <div className="mt-2 flex items-center gap-3 text-xs text-muted">
            <span>Settlement: {leg.settlement_preference}</span>
            <span>|</span>
            <span>Order Group: {leg.order_group}</span>
            {leg.open_sequence && <><span>|</span><span>Sequence: {leg.open_sequence}</span></>}
          </div>
        </div>

        <div className="flex gap-2 ml-4">
          <Button size="sm" variant="secondary" onClick={onEdit}>
            Edit
          </Button>
          <Button size="sm" variant="danger" onClick={onRemove}>
            Remove
          </Button>
        </div>
      </div>
    </Card>
  )
}
```

### LegEditor.tsx — Leg edit form (inline editing)

```typescript
import { useState } from 'react'
import type { StrategyLeg } from '../../../types/sdf-v1'
import { useWizardStore } from '../../../stores/wizardStore'
import { Card } from '../../ui/Card'
import { Button } from '../../ui/Button'
import { Input } from '../../ui/Input'
import { Select } from '../../ui/Select'

interface LegEditorProps {
  leg: StrategyLeg
  type: 'legs' | 'protection_legs'
  onSave: () => void
  onCancel: () => void
}

export function LegEditor({ leg, type, onSave, onCancel }: LegEditorProps) {
  const { setField, draft } = useWizardStore()
  const legs = type === 'legs' ? (draft.structure?.legs ?? []) : (draft.structure?.protection_legs ?? [])
  const legIndex = legs.findIndex(l => l.leg_id === leg.leg_id)

  const basePath = `structure.${type}.${legIndex}`

  const handleFieldChange = (field: keyof StrategyLeg, value: unknown) => {
    setField(`${basePath}.${field}`, value)
  }

  return (
    <Card className="p-4 bg-elevated border-2 border-primary">
      <div className="space-y-4">
        <div className="flex items-center justify-between mb-3">
          <h4 className="font-semibold">Edit Leg: {leg.leg_id}</h4>
          <div className="flex gap-2">
            <Button size="sm" variant="secondary" onClick={onCancel}>
              Cancel
            </Button>
            <Button size="sm" onClick={onSave}>
              Save
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          {/* Action */}
          <Select
            label="Action"
            value={leg.action}
            onChange={(e) => handleFieldChange('action', e.target.value)}
          >
            <option value="buy">Buy</option>
            <option value="sell">Sell</option>
          </Select>

          {/* Right */}
          <Select
            label="Right"
            value={leg.right}
            onChange={(e) => handleFieldChange('right', e.target.value)}
          >
            <option value="put">Put</option>
            <option value="call">Call</option>
          </Select>

          {/* Target Delta */}
          <Input
            label="Target Delta"
            type="number"
            step="0.01"
            min="0.01"
            max="0.99"
            value={leg.target_delta}
            onChange={(e) => handleFieldChange('target_delta', parseFloat(e.target.value))}
          />

          {/* Delta Tolerance */}
          <Input
            label="Delta Tolerance"
            type="number"
            step="0.01"
            min="0"
            max="0.5"
            value={leg.delta_tolerance}
            onChange={(e) => handleFieldChange('delta_tolerance', parseFloat(e.target.value))}
          />

          {/* Target DTE */}
          <Input
            label="Target DTE"
            type="number"
            step="1"
            min="1"
            max="365"
            value={leg.target_dte}
            onChange={(e) => handleFieldChange('target_dte', parseInt(e.target.value))}
          />

          {/* DTE Tolerance */}
          <Input
            label="DTE Tolerance"
            type="number"
            step="1"
            min="0"
            max="30"
            value={leg.dte_tolerance}
            onChange={(e) => handleFieldChange('dte_tolerance', parseInt(e.target.value))}
          />

          {/* Quantity */}
          <Input
            label="Quantity"
            type="number"
            step="1"
            min="1"
            max="100"
            value={leg.quantity}
            onChange={(e) => handleFieldChange('quantity', parseInt(e.target.value))}
          />

          {/* Settlement Preference */}
          <Select
            label="Settlement"
            value={leg.settlement_preference}
            onChange={(e) => handleFieldChange('settlement_preference', e.target.value)}
          >
            <option value="PM">PM-settled</option>
            <option value="AM">AM-settled</option>
          </Select>

          {/* Role */}
          <Input
            label="Role"
            type="text"
            placeholder="e.g., short put, long call hedge"
            value={leg.role}
            onChange={(e) => handleFieldChange('role', e.target.value)}
          />

          {/* Order Group */}
          <Select
            label="Order Group"
            value={leg.order_group}
            onChange={(e) => handleFieldChange('order_group', e.target.value)}
          >
            <option value="combo">Combo</option>
            <option value="standalone">Standalone</option>
          </Select>

          {/* Exclude Expiry Within Days */}
          <Input
            label="Exclude Expiry Within Days"
            type="number"
            step="1"
            min="0"
            max="30"
            value={leg.exclude_expiry_within_days}
            onChange={(e) => handleFieldChange('exclude_expiry_within_days', parseInt(e.target.value))}
          />

          {/* Open Sequence (optional) */}
          <Input
            label="Open Sequence (optional)"
            type="text"
            placeholder="e.g., 1, 2"
            value={leg.open_sequence ?? ''}
            onChange={(e) => handleFieldChange('open_sequence', e.target.value || undefined)}
          />
        </div>
      </div>
    </Card>
  )
}
```

## Test Requirements

Create `dashboard/src/components/strategy-wizard/steps/LegsStep.test.tsx`:

- **TEST-03-01**: Render LegsStep with no legs → mostra messaggio "Nessun leg configurato"
- **TEST-03-02**: Click "+ Aggiungi Leg" → nuovo leg aggiunto a `draft.structure.legs`
- **TEST-03-03**: Click "+ Aggiungi Protection" → nuovo leg aggiunto a `draft.structure.protection_legs`
- **TEST-03-04**: Click "Edit" su LegCard → mostra LegEditor
- **TEST-03-05**: Edit delta field in LegEditor → `setField` chiamato con path corretto
- **TEST-03-06**: Click "Save" in LegEditor → editor chiuso, mostra LegCard
- **TEST-03-07**: Click "Remove" su LegCard → leg rimosso da array
- **TEST-03-08**: LegCard mostra badge corretto per action=buy → badge verde "BUY"
- **TEST-03-09**: LegCard mostra badge corretto per right=call → badge blu "CALL"
- **TEST-03-10**: Summary mostra conteggio corretto legs → "2 primary legs, 1 protection leg"

## Integration in StrategyWizardPage

Modify `StrategyWizardPage.tsx` to show LegsStep when `currentStep === 5`:

```typescript
import { LegsStep } from '../components/strategy-wizard/steps/LegsStep'

// Inside render:
{currentStep === 5 && <LegsStep />}
```

## Done Criteria
- [ ] Build pulito (`bun run build` o `npm run build` → 0 errori TypeScript)
- [ ] Tutti i test TEST-03-XX passano (10/10)
- [ ] LegsStep integrato in StrategyWizardPage
- [ ] UI rendering senza errori console
- [ ] Aggiungi/Edit/Rimuovi leg funzionanti
- [ ] Validazione inline mostra errori delta/DTE fuori range
- [ ] Summary mostra conteggio corretto

## Stima
~1 giorno
