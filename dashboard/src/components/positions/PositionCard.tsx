// PositionCard — compact card variant for the Positions grid view. Renders
// the symbol + strategy/status chips on the left and a large P&L block on
// the right, with a 2x2 metrics grid (Qty/DTE/Δ/Θ) in the footer.
import { Card } from '../ui/Card'
import { Badge, type BadgeTone } from '../ui/Badge'
import type { Position } from '../../types/position'
import { formatCurrency } from '../../utils/format'

const statusToneMap: Record<Position['status'], BadgeTone> = {
  open: 'green',
  closed: 'muted',
  pending: 'yellow',
}

function daysToExpiration(position: Position): number {
  if (!position.expiration) return 0
  const exp = new Date(position.expiration).getTime()
  const now = Date.now()
  const diff = Math.round((exp - now) / (1000 * 60 * 60 * 24))
  return Math.max(0, diff)
}

export function PositionCard({ position }: { position: Position }) {
  const up = (position.unrealizedPnl ?? 0) >= 0
  const statusTone = statusToneMap[position.status]
  const pnlMagnitude = formatCurrency(
    Math.abs(position.unrealizedPnl ?? 0),
    'USD',
    0
  )
  // Re-prefix sign so "+" aligns visually with the up-arrow and avoids the
  // default "-" from formatCurrency for negative amounts
  const pnlDisplay = up ? `+${pnlMagnitude}` : `-${pnlMagnitude}`

  const dte = daysToExpiration(position)
  const delta = position.greeks?.delta
  const theta = position.greeks?.theta

  return (
    <Card padding={16}>
      <div className="flex justify-between items-start mb-2.5">
        <div>
          <div className="font-mono font-semibold text-[13px]">
            {position.symbol}
          </div>
          <div className="flex gap-1 mt-1.5">
            {position.strategy && (
              <Badge tone="purple" size="sm">
                {position.strategy}
              </Badge>
            )}
            <Badge tone={statusTone} size="sm">
              {position.status.toUpperCase()}
            </Badge>
          </div>
        </div>
        <div className="text-right">
          <div
            className={`font-mono text-[16px] font-semibold tabular-nums ${
              up ? 'text-up' : 'text-down'
            }`}
          >
            {pnlDisplay}
          </div>
          <div className={`text-[11px] ${up ? 'text-up' : 'text-down'}`}>
            {up ? '▲' : '▼'}{' '}
            {Math.abs(position.unrealizedPnlPct ?? 0).toFixed(2)}%
          </div>
        </div>
      </div>
      <div className="grid grid-cols-2 gap-2 text-[11.5px] pt-2.5 border-t border-border">
        <div>
          <span className="text-muted">Qty</span>{' '}
          <span className="font-mono">{position.quantity}</span>
        </div>
        <div>
          <span className="text-muted">DTE</span>{' '}
          <span className="font-mono">{dte}d</span>
        </div>
        <div>
          <span className="text-muted">Δ</span>{' '}
          <span className="font-mono">
            {delta !== undefined ? delta.toFixed(2) : '—'}
          </span>
        </div>
        <div>
          <span className="text-muted">Θ</span>{' '}
          <span className="font-mono">
            {theta !== undefined ? theta.toFixed(2) : '—'}
          </span>
        </div>
      </div>
    </Card>
  )
}
