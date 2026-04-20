import type { Position } from '../../types/position'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { formatDistance } from 'date-fns'
import { cn } from '../../utils/cn'
import { TrendingUp, TrendingDown } from 'lucide-react'

interface PositionCardProps {
  position: Position
}

export function PositionCard({ position }: PositionCardProps) {
  const isProfitable = position.unrealizedPnl >= 0
  const pnlColor = isProfitable ? 'text-success' : 'text-danger'

  const statusVariant =
    position.status === 'open' ? 'success' : position.status === 'closed' ? 'default' : 'warning'

  const typeLabel = position.type === 'option' ? 'OPT' : position.type === 'stock' ? 'STK' : 'FUT'

  return (
    <Card className="hover:shadow-lg transition-shadow">
      <CardHeader>
        <div className="flex items-start justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              {position.symbol}
              <Badge variant={statusVariant}>{position.status.toUpperCase()}</Badge>
            </CardTitle>
            {position.strategy && (
              <p className="text-sm text-muted mt-1">{position.strategy}</p>
            )}
          </div>
          <div className="flex gap-2">
            <Badge>{typeLabel}</Badge>
            <Badge variant={position.side === 'long' ? 'success' : 'danger'}>
              {position.side.toUpperCase()}
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* P&L Section */}
        <div className="flex items-center justify-between p-3 bg-muted/5 rounded-lg">
          <div>
            <p className="text-xs text-muted mb-1">Unrealized P&L</p>
            <div className="flex items-center gap-2">
              {isProfitable ? (
                <TrendingUp className="h-4 w-4 text-success" />
              ) : (
                <TrendingDown className="h-4 w-4 text-danger" />
              )}
              <span className={cn('text-2xl font-bold', pnlColor)}>
                ${Math.abs(position.unrealizedPnl).toFixed(2)}
              </span>
            </div>
          </div>
          <div className="text-right">
            <p className="text-xs text-muted mb-1">Percentage</p>
            <span className={cn('text-xl font-semibold', pnlColor)}>
              {isProfitable ? '+' : ''}
              {position.unrealizedPnlPct.toFixed(2)}%
            </span>
          </div>
        </div>

        {/* Position Details */}
        <div className="grid grid-cols-2 gap-3 text-sm">
          <div>
            <p className="text-muted">Quantity</p>
            <p className="font-semibold">{position.quantity}</p>
          </div>
          <div>
            <p className="text-muted">Market Value</p>
            <p className="font-semibold">${position.marketValue.toFixed(2)}</p>
          </div>
          <div>
            <p className="text-muted">Avg Cost</p>
            <p className="font-semibold">${position.avgCost.toFixed(2)}</p>
          </div>
          <div>
            <p className="text-muted">Current Price</p>
            <p className="font-semibold">${position.currentPrice.toFixed(2)}</p>
          </div>
        </div>

        {/* Option Details */}
        {position.type === 'option' && position.strike && position.expiration && position.right && (
          <div className="pt-3 border-t border-border">
            <p className="text-xs text-muted mb-2">Option Details</p>
            <div className="grid grid-cols-3 gap-2 text-sm">
              <div>
                <p className="text-muted text-xs">Strike</p>
                <p className="font-semibold">${position.strike}</p>
              </div>
              <div>
                <p className="text-muted text-xs">Type</p>
                <p className="font-semibold uppercase">{position.right}</p>
              </div>
              <div>
                <p className="text-muted text-xs">Expiry</p>
                <p className="font-semibold">{new Date(position.expiration).toLocaleDateString()}</p>
              </div>
            </div>
          </div>
        )}

        {/* Greeks */}
        {position.greeks && (
          <div className="pt-3 border-t border-border">
            <p className="text-xs text-muted mb-2">Greeks</p>
            <div className="grid grid-cols-4 gap-2 text-sm">
              <div>
                <p className="text-muted text-xs">Delta</p>
                <p className="font-semibold">{position.greeks.delta.toFixed(3)}</p>
              </div>
              <div>
                <p className="text-muted text-xs">Gamma</p>
                <p className="font-semibold">{position.greeks.gamma.toFixed(3)}</p>
              </div>
              <div>
                <p className="text-muted text-xs">Theta</p>
                <p className="font-semibold">{position.greeks.theta.toFixed(3)}</p>
              </div>
              <div>
                <p className="text-muted text-xs">Vega</p>
                <p className="font-semibold">{position.greeks.vega.toFixed(3)}</p>
              </div>
            </div>
            {position.greeks.iv !== undefined && (
              <div className="mt-2">
                <p className="text-muted text-xs">Implied Volatility</p>
                <p className="font-semibold">{(position.greeks.iv * 100).toFixed(1)}%</p>
              </div>
            )}
          </div>
        )}

        {/* Footer */}
        <div className="pt-3 border-t border-border text-xs text-muted">
          <p>
            Opened {formatDistance(new Date(position.openDate), new Date(), { addSuffix: true })}
          </p>
          {position.closeDate && (
            <p className="mt-1">
              Closed {formatDistance(new Date(position.closeDate), new Date(), { addSuffix: true })}
            </p>
          )}
        </div>
      </CardContent>
    </Card>
  )
}
