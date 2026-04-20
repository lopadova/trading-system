import type { PositionsResponse } from '../../types/position'
import { Card, CardContent, CardHeader, CardTitle } from '../ui/Card'
import { cn } from '../../utils/cn'
import { TrendingUp, TrendingDown, DollarSign, Briefcase } from 'lucide-react'

interface PositionsSummaryProps {
  data: PositionsResponse
}

export function PositionsSummary({ data }: PositionsSummaryProps) {
  const { summary } = data
  const isProfitable = summary.totalUnrealizedPnl >= 0

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <Briefcase className="h-4 w-4 text-muted" />
            Total Positions
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">{summary.totalPositions}</p>
          <div className="flex gap-4 mt-2 text-sm text-muted">
            <span>{summary.openPositions} open</span>
            <span>{summary.closedPositions} closed</span>
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <DollarSign className="h-4 w-4 text-muted" />
            Market Value
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-3xl font-bold">${summary.totalMarketValue.toFixed(2)}</p>
          <p className="text-sm text-muted mt-2">Open positions</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            {isProfitable ? (
              <TrendingUp className="h-4 w-4 text-success" />
            ) : (
              <TrendingDown className="h-4 w-4 text-danger" />
            )}
            Unrealized P&L
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p
            className={cn(
              'text-3xl font-bold',
              isProfitable ? 'text-success' : 'text-danger'
            )}
          >
            {isProfitable ? '+' : ''}${summary.totalUnrealizedPnl.toFixed(2)}
          </p>
          <p className="text-sm text-muted mt-2">Total gain/loss</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            {isProfitable ? (
              <TrendingUp className="h-4 w-4 text-success" />
            ) : (
              <TrendingDown className="h-4 w-4 text-danger" />
            )}
            P&L Percentage
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p
            className={cn(
              'text-3xl font-bold',
              isProfitable ? 'text-success' : 'text-danger'
            )}
          >
            {isProfitable ? '+' : ''}
            {summary.totalUnrealizedPnlPct.toFixed(2)}%
          </p>
          <p className="text-sm text-muted mt-2">Return on investment</p>
        </CardContent>
      </Card>
    </div>
  )
}
