/**
 * LegEditor Component
 *
 * Inline editor for modifying strategy leg parameters.
 * Uses wizardStore.setField to update nested leg properties.
 */

import type { StrategyLeg, OptionRight } from '../../../types/sdf-v1'
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

  // Find the index of this leg in the appropriate array
  const legs =
    type === 'legs'
      ? (draft.structure?.legs ?? [])
      : (draft.structure?.protection_legs ?? [])
  const legIndex = legs.findIndex((l) => l?.leg_id === leg.leg_id)

  // Base path for setField
  const basePath = `structure.${type}.${legIndex}`

  // Field change handler
  const handleFieldChange = (field: keyof StrategyLeg, value: unknown) => {
    setField(`${basePath}.${field}`, value)
  }

  return (
    <Card className="p-4 bg-elevated border-2 border-primary">
      <div className="space-y-4">
        {/* Header with Save/Cancel */}
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

        {/* Form fields in grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {/* Action */}
          <Select
            label="Action"
            value={leg.action}
            options={[
              { value: 'buy', label: 'Buy' },
              { value: 'sell', label: 'Sell' },
            ]}
            onChange={(value) => handleFieldChange('action', value)}
          />

          {/* Right */}
          <Select
            label="Right"
            value={leg.right}
            options={[
              { value: 'put', label: 'Put' },
              { value: 'call', label: 'Call' },
            ]}
            onChange={(value) => handleFieldChange('right', value as OptionRight)}
          />

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
            label="Target DTE (days)"
            type="number"
            step="1"
            min="1"
            max="365"
            value={leg.target_dte}
            onChange={(e) => handleFieldChange('target_dte', parseInt(e.target.value, 10))}
          />

          {/* DTE Tolerance */}
          <Input
            label="DTE Tolerance (days)"
            type="number"
            step="1"
            min="0"
            max="30"
            value={leg.dte_tolerance}
            onChange={(e) => handleFieldChange('dte_tolerance', parseInt(e.target.value, 10))}
          />

          {/* Quantity */}
          <Input
            label="Quantity"
            type="number"
            step="1"
            min="1"
            max="100"
            value={leg.quantity}
            onChange={(e) => handleFieldChange('quantity', parseInt(e.target.value, 10))}
          />

          {/* Settlement Preference */}
          <Select
            label="Settlement"
            value={leg.settlement_preference}
            options={[
              { value: 'PM', label: 'PM-settled' },
              { value: 'AM', label: 'AM-settled' },
            ]}
            onChange={(value) => handleFieldChange('settlement_preference', value)}
          />

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
            options={[
              { value: 'combo', label: 'Combo' },
              { value: 'standalone', label: 'Standalone' },
            ]}
            onChange={(value) => handleFieldChange('order_group', value)}
          />

          {/* Exclude Expiry Within Days */}
          <Input
            label="Exclude Expiry Within Days"
            type="number"
            step="1"
            min="0"
            max="30"
            value={leg.exclude_expiry_within_days}
            onChange={(e) =>
              handleFieldChange('exclude_expiry_within_days', parseInt(e.target.value, 10))
            }
          />

          {/* Open Sequence (optional) */}
          <Input
            label="Open Sequence (optional)"
            type="text"
            placeholder="e.g., 1, 2"
            value={leg.open_sequence ?? ''}
            onChange={(e) =>
              handleFieldChange('open_sequence', e.target.value || undefined)
            }
          />
        </div>
      </div>
    </Card>
  )
}
