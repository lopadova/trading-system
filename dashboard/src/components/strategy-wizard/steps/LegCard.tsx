/**
 * LegCard Component
 *
 * Displays a strategy leg in a read-only card format.
 * Shows key leg parameters with color-coded badges for action and right.
 */

import type { StrategyLeg } from '../../../types/sdf-v1'
import { Card } from '../../ui/Card'
import { Button } from '../../ui/Button'
import { Badge, type BadgeTone } from '../../ui/Badge'

interface LegCardProps {
  leg: StrategyLeg
  onEdit: () => void
  onRemove: () => void
}

export function LegCard({ leg, onEdit, onRemove }: LegCardProps) {
  const actionTone: BadgeTone = leg.action === 'buy' ? 'green' : 'red'
  const rightTone: BadgeTone = leg.right === 'put' ? 'yellow' : 'muted'

  return (
    <Card className="p-4">
      <div className="flex items-start justify-between">
        <div className="flex-1">
          {/* Header with badges and leg ID */}
          <div className="flex items-center gap-2 mb-3">
            <Badge tone={actionTone}>{leg.action.toUpperCase()}</Badge>
            <Badge tone={rightTone}>{leg.right.toUpperCase()}</Badge>
            <span className="font-mono text-sm text-muted">{leg.leg_id}</span>
          </div>

          {/* Key parameters grid */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            <div>
              <div className="text-muted text-xs">Target Delta</div>
              <div className="font-semibold">
                {leg.target_delta} ± {leg.delta_tolerance}
              </div>
            </div>
            <div>
              <div className="text-muted text-xs">Target DTE</div>
              <div className="font-semibold">
                {leg.target_dte} ± {leg.dte_tolerance}
              </div>
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

          {/* Additional details */}
          <div className="mt-3 flex items-center gap-3 text-xs text-muted">
            <span>Settlement: {leg.settlement_preference}</span>
            <span>|</span>
            <span>Order Group: {leg.order_group}</span>
            {leg.open_sequence && (
              <>
                <span>|</span>
                <span>Sequence: {leg.open_sequence}</span>
              </>
            )}
          </div>
        </div>

        {/* Action buttons */}
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
