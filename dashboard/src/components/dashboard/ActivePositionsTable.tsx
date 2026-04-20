// ActivePositionsTable — compact table of currently-open positions. Column
// layout tracks the dashboard kit: Symbol (+ strategy badge) · Campaign · Qty
// · Mark · P&L · % · DTE (days to expiry).

import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Skeleton'
import { RefreshCw, Plus } from 'lucide-react'
import { usePositions } from '../../hooks/usePositions'
import { formatCurrency } from '../../utils/format'
import type { Position } from '../../types/position'

interface Props {
  onNewCampaign?: () => void
}

// Derive days-to-expiration from an ISO date string. Returns 0 if the input
// is missing or already past.
function daysToExpiration(expiration?: string): number {
  if (!expiration) return 0
  const ms = new Date(expiration).getTime() - Date.now()
  if (Number.isNaN(ms) || ms <= 0) return 0
  return Math.ceil(ms / (1000 * 60 * 60 * 24))
}

export function ActivePositionsTable({ onNewCampaign }: Props) {
  // Only fetch open positions; PositionStatus is lowercase in the domain type
  const { data, isLoading, refetch, isFetching } = usePositions({ status: 'open' })
  const positions: Position[] = data?.positions ?? []

  return (
    <Card padding={0}>
      <div className="px-5 py-4 border-b border-border flex justify-between items-center">
        <div>
          <h3 className="m-0 text-[15px] font-semibold">Active Positions</h3>
          <div className="text-[12px] text-muted mt-0.5">{positions.length} open</div>
        </div>
        <div className="flex gap-2">
          <Button
            variant="secondary"
            size="sm"
            icon={RefreshCw}
            loading={isFetching}
            onClick={() => refetch()}
          >
            Refresh
          </Button>
          <Button variant="primary" size="sm" icon={Plus} onClick={onNewCampaign}>
            New Campaign
          </Button>
        </div>
      </div>
      {isLoading ? (
        <div className="p-5">
          <Skeleton h={140} />
        </div>
      ) : (
        <table className="w-full border-collapse">
          <thead>
            <tr className="bg-[var(--bg-1)]">
              {['Symbol', 'Campaign', 'Qty', 'Mark', 'P&L', '%', 'DTE'].map((h, i) => (
                <th
                  key={h}
                  className={`px-3 py-2.5 text-[11px] font-medium text-muted uppercase tracking-wider border-b border-border ${
                    i < 2 ? 'text-left' : 'text-right'
                  }`}
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {positions.map(p => {
              const up = p.unrealizedPnl >= 0
              const dte = daysToExpiration(p.expiration)
              return (
                <tr key={p.id} className="border-b border-border last:border-0">
                  <td className="px-3 py-2.5">
                    <div className="flex items-center gap-2">
                      <span className="font-mono font-semibold">{p.symbol}</span>
                      {p.strategy && (
                        <Badge tone="purple" size="sm">
                          {p.strategy}
                        </Badge>
                      )}
                    </div>
                  </td>
                  <td className="px-3 py-2.5 text-[12px]">
                    {p.campaign ? (
                      <span className="text-accent font-medium">{p.campaign}</span>
                    ) : (
                      <span className="text-subtle">—</span>
                    )}
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono tabular-nums text-muted">
                    {p.quantity}
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono tabular-nums">
                    {formatCurrency(p.currentPrice, 'USD')}
                  </td>
                  <td
                    className={`px-3 py-2.5 text-right font-mono tabular-nums font-medium ${up ? 'text-up' : 'text-down'}`}
                  >
                    {formatCurrency(p.unrealizedPnl, 'USD')}
                  </td>
                  <td
                    className={`px-3 py-2.5 text-right text-[12px] ${up ? 'text-up' : 'text-down'}`}
                  >
                    {up ? '▲' : '▼'} {Math.abs(p.unrealizedPnlPct).toFixed(2)}%
                  </td>
                  <td className="px-3 py-2.5 text-right font-mono text-muted">{dte}d</td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}
    </Card>
  )
}
