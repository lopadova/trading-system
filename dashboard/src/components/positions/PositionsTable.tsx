// PositionsTable — flat 10-column table styled with kit tokens. Columns:
// Symbol · Status · Qty · Cost · Mark · P&L · % · Δ · Θ · DTE.
// P&L coloring uses kit text-up/text-down; status tone mirrors the status
// chip palette on the filter bar.
import { Badge, type BadgeTone } from '../ui/Badge'
import type { Position } from '../../types/position'
import { formatCurrency } from '../../utils/format'

interface Props {
  positions: Position[]
}

// Compute days-to-expiration from the option expiration string (ISO date).
// Returns 0 for stocks/futures (no expiration) or past expirations.
function daysToExpiration(position: Position): number {
  if (!position.expiration) return 0
  const exp = new Date(position.expiration).getTime()
  const now = Date.now()
  const diff = Math.round((exp - now) / (1000 * 60 * 60 * 24))
  return Math.max(0, diff)
}

const statusToneMap: Record<Position['status'], BadgeTone> = {
  open: 'green',
  closed: 'muted',
  pending: 'yellow',
}

export function PositionsTable({ positions }: Props) {
  if (positions.length === 0) {
    return (
      <div className="text-center py-12 text-muted">No positions match</div>
    )
  }

  const headers = ['Symbol', 'Status', 'Qty', 'Cost', 'Mark', 'P&L', '%', 'Δ', 'Θ', 'DTE']

  return (
    <table className="w-full border-collapse">
      <thead>
        <tr className="bg-[var(--bg-1)]">
          {headers.map((header, i) => (
            <th
              key={header}
              className={`px-3 py-2.5 text-[11px] font-medium text-muted uppercase tracking-wider border-b border-border ${
                i < 2 ? 'text-left' : 'text-right'
              }`}
            >
              {header}
            </th>
          ))}
        </tr>
      </thead>
      <tbody>
        {positions.map(position => {
          const up = (position.unrealizedPnl ?? 0) >= 0
          const statusTone: BadgeTone = statusToneMap[position.status]
          const dte = daysToExpiration(position)
          const delta = position.greeks?.delta
          const theta = position.greeks?.theta

          return (
            <tr
              key={position.id}
              className="border-b border-border last:border-0"
            >
              <td className="px-3 py-2.5">
                <div className="flex items-center gap-2">
                  <span className="font-mono font-semibold">
                    {position.symbol}
                  </span>
                  {position.strategy && (
                    <Badge tone="purple" size="sm">
                      {position.strategy}
                    </Badge>
                  )}
                </div>
              </td>
              <td className="px-3 py-2.5">
                <Badge tone={statusTone} size="sm">
                  {position.status.toUpperCase()}
                </Badge>
              </td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">
                {position.quantity}
              </td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">
                {formatCurrency(position.avgCost ?? 0, 'USD')}
              </td>
              <td className="px-3 py-2.5 text-right font-mono tabular-nums">
                {formatCurrency(position.currentPrice ?? 0, 'USD')}
              </td>
              <td
                className={`px-3 py-2.5 text-right font-mono tabular-nums font-medium ${
                  up ? 'text-up' : 'text-down'
                }`}
              >
                {formatCurrency(position.unrealizedPnl ?? 0, 'USD')}
              </td>
              <td
                className={`px-3 py-2.5 text-right text-[12px] ${
                  up ? 'text-up' : 'text-down'
                }`}
              >
                {up ? '▲' : '▼'}{' '}
                {Math.abs(position.unrealizedPnlPct ?? 0).toFixed(2)}%
              </td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">
                {delta !== undefined ? delta.toFixed(2) : '—'}
              </td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">
                {theta !== undefined ? theta.toFixed(2) : '—'}
              </td>
              <td className="px-3 py-2.5 text-right font-mono text-muted">
                {dte}d
              </td>
            </tr>
          )
        })}
      </tbody>
    </table>
  )
}
