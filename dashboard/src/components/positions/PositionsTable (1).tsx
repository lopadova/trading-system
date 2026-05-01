import type { Position } from '../../types/position'
import { Badge } from '../ui/Badge'
import { cn } from '../../utils/cn'
import { ArrowUpRight, ArrowDownRight } from 'lucide-react'

interface PositionsTableProps {
  positions: Position[]
}

export function PositionsTable({ positions }: PositionsTableProps) {
  if (positions.length === 0) {
    return (
      <div className="text-center py-12">
        <p className="text-muted text-lg">No positions found</p>
        <p className="text-muted text-sm mt-2">Adjust your filters or wait for new positions</p>
      </div>
    )
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-border">
            <th className="text-left py-3 px-4 font-semibold text-foreground">Symbol</th>
            <th className="text-left py-3 px-4 font-semibold text-foreground">Strategy</th>
            <th className="text-left py-3 px-4 font-semibold text-foreground">Type</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">Quantity</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">Avg Cost</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">Current</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">Market Value</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">P&L</th>
            <th className="text-right py-3 px-4 font-semibold text-foreground">P&L %</th>
            <th className="text-center py-3 px-4 font-semibold text-foreground">Status</th>
          </tr>
        </thead>
        <tbody>
          {positions.map((position) => {
            const isProfitable = position.unrealizedPnl >= 0
            const pnlColor = isProfitable ? 'text-success' : 'text-danger'
            const statusVariant =
              position.status === 'open'
                ? 'success'
                : position.status === 'closed'
                  ? 'default'
                  : 'warning'

            return (
              <tr
                key={position.id}
                className="border-b border-border hover:bg-muted/5 transition-colors"
              >
                <td className="py-3 px-4">
                  <div>
                    <p className="font-semibold">{position.symbol}</p>
                    {position.type === 'option' && position.strike && position.right && (
                      <p className="text-xs text-muted">
                        ${position.strike} {position.right.toUpperCase()}
                      </p>
                    )}
                  </div>
                </td>
                <td className="py-3 px-4">
                  <span className="text-muted">{position.strategy || '-'}</span>
                </td>
                <td className="py-3 px-4">
                  <Badge>
                    {position.type === 'option' ? 'OPT' : position.type === 'stock' ? 'STK' : 'FUT'}
                  </Badge>
                </td>
                <td className="py-3 px-4 text-right font-mono">{position.quantity}</td>
                <td className="py-3 px-4 text-right font-mono">${position.avgCost.toFixed(2)}</td>
                <td className="py-3 px-4 text-right font-mono">
                  ${position.currentPrice.toFixed(2)}
                </td>
                <td className="py-3 px-4 text-right font-mono">
                  ${position.marketValue.toFixed(2)}
                </td>
                <td className={cn('py-3 px-4 text-right font-mono font-semibold', pnlColor)}>
                  <div className="flex items-center justify-end gap-1">
                    {isProfitable ? (
                      <ArrowUpRight className="h-3 w-3" />
                    ) : (
                      <ArrowDownRight className="h-3 w-3" />
                    )}
                    {isProfitable ? '+' : ''}${position.unrealizedPnl.toFixed(2)}
                  </div>
                </td>
                <td className={cn('py-3 px-4 text-right font-mono font-semibold', pnlColor)}>
                  {isProfitable ? '+' : ''}
                  {position.unrealizedPnlPct.toFixed(2)}%
                </td>
                <td className="py-3 px-4 text-center">
                  <Badge variant={statusVariant}>{position.status.toUpperCase()}</Badge>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}
