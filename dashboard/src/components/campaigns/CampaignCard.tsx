// Campaign card component
// Displays individual campaign information in a card layout

import type { Campaign } from '../../types/campaign'
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card'
import { Badge } from '../ui/Badge'
import {
  Calendar,
  DollarSign,
  TrendingUp,
  TrendingDown,
  Activity,
  Play,
  Pause,
  Archive,
} from 'lucide-react'
import { format, parseISO } from 'date-fns'

interface CampaignCardProps {
  campaign: Campaign
  onStatusChange?: (id: string, status: 'active' | 'paused') => void
  onClick?: (id: string) => void
}

function getStatusBadgeVariant(status: Campaign['status']): 'default' | 'success' | 'warning' | 'danger' {
  switch (status) {
    case 'active':
      return 'success'
    case 'paused':
      return 'warning'
    case 'closed':
      return 'default'
    case 'pending':
      return 'warning'
  }
}

function getStatusIcon(status: Campaign['status']) {
  switch (status) {
    case 'active':
      return <Play className="h-3 w-3" />
    case 'paused':
      return <Pause className="h-3 w-3" />
    case 'closed':
      return <Archive className="h-3 w-3" />
    case 'pending':
      return <Activity className="h-3 w-3" />
  }
}

function getStrategyLabel(strategyType: Campaign['strategyType']): string {
  const labels: Record<Campaign['strategyType'], string> = {
    put_spread: 'Put Spread',
    call_spread: 'Call Spread',
    iron_condor: 'Iron Condor',
    naked_put: 'Naked Put',
    covered_call: 'Covered Call',
    other: 'Other',
  }
  return labels[strategyType]
}

export function CampaignCard({ campaign, onStatusChange, onClick }: CampaignCardProps) {
  const pnlPositive = campaign.pnl.total >= 0
  const startDate = format(parseISO(campaign.startDate), 'MMM dd, yyyy')
  const endDate = campaign.endDate ? format(parseISO(campaign.endDate), 'MMM dd, yyyy') : null

  const handleCardClick = () => {
    onClick?.(campaign.id)
  }

  return (
    <div onClick={handleCardClick} className="cursor-pointer">
      <Card className="hover:shadow-lg transition-shadow">
      <CardHeader>
        <div className="flex items-start justify-between gap-2">
          <div className="flex-1 min-w-0">
            <CardTitle className="text-base truncate">{campaign.name}</CardTitle>
            <div className="flex items-center gap-2 mt-1">
              <Badge variant={getStatusBadgeVariant(campaign.status)}>
                <div className="flex items-center gap-1">
                  {getStatusIcon(campaign.status)}
                  <span className="capitalize">{campaign.status}</span>
                </div>
              </Badge>
              <span className="text-xs text-muted">{campaign.underlying}</span>
            </div>
          </div>
        </div>
      </CardHeader>

      <CardContent className="space-y-4">
        {/* Strategy Type */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-muted">Strategy</span>
          <span className="font-medium">{getStrategyLabel(campaign.strategyType)}</span>
        </div>

        {/* Dates */}
        <div className="flex items-center gap-2 text-xs text-muted">
          <Calendar className="h-3 w-3" />
          <span>
            {startDate}
            {endDate && ` - ${endDate}`}
          </span>
        </div>

        {/* P&L */}
        <div className="space-y-2">
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted">Total P&L</span>
            <div className="flex items-center gap-1">
              {pnlPositive ? (
                <TrendingUp className="h-4 w-4 text-success" />
              ) : (
                <TrendingDown className="h-4 w-4 text-danger" />
              )}
              <span className={`text-lg font-bold ${pnlPositive ? 'text-success' : 'text-danger'}`}>
                {pnlPositive ? '+' : ''}${campaign.pnl.total.toFixed(0)}
              </span>
            </div>
          </div>
          <div className="text-right">
            <span className={`text-sm ${pnlPositive ? 'text-success' : 'text-danger'}`}>
              {pnlPositive ? '+' : ''}
              {campaign.pnl.totalPct.toFixed(2)}%
            </span>
          </div>
        </div>

        {/* Capital & Current Value */}
        <div className="grid grid-cols-2 gap-4 pt-2 border-t border-border">
          <div>
            <div className="flex items-center gap-1 text-xs text-muted mb-1">
              <DollarSign className="h-3 w-3" />
              <span>Capital</span>
            </div>
            <div className="text-sm font-medium">${campaign.capital.toLocaleString()}</div>
          </div>
          <div>
            <div className="text-xs text-muted mb-1">Current</div>
            <div className="text-sm font-medium">${campaign.currentValue.toLocaleString()}</div>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-3 gap-2 pt-2 border-t border-border text-xs">
          <div className="text-center">
            <div className="text-muted mb-1">Win Rate</div>
            <div className="font-semibold">{(campaign.stats.winRate * 100).toFixed(0)}%</div>
          </div>
          <div className="text-center">
            <div className="text-muted mb-1">Trades</div>
            <div className="font-semibold">{campaign.stats.totalTrades}</div>
          </div>
          <div className="text-center">
            <div className="text-muted mb-1">Open</div>
            <div className="font-semibold">{campaign.stats.openPositions}</div>
          </div>
        </div>

        {/* Action Buttons */}
        {campaign.status !== 'closed' && onStatusChange && (
          <div className="pt-2 border-t border-border">
            {campaign.status === 'active' ? (
              <button
                onClick={(e) => {
                  e.stopPropagation()
                  onStatusChange(campaign.id, 'paused')
                }}
                className="w-full px-3 py-2 text-sm font-medium bg-warning/10 text-warning rounded-lg hover:bg-warning/20 transition-colors flex items-center justify-center gap-2"
              >
                <Pause className="h-4 w-4" />
                Pause Campaign
              </button>
            ) : (
              <button
                onClick={(e) => {
                  e.stopPropagation()
                  onStatusChange(campaign.id, 'active')
                }}
                className="w-full px-3 py-2 text-sm font-medium bg-success/10 text-success rounded-lg hover:bg-success/20 transition-colors flex items-center justify-center gap-2"
              >
                <Play className="h-4 w-4" />
                Resume Campaign
              </button>
            )}
          </div>
        )}

        {/* Tags */}
        {campaign.tags && campaign.tags.length > 0 && (
          <div className="flex flex-wrap gap-1">
            {campaign.tags.map((tag) => (
              <span
                key={tag}
                className="text-xs px-2 py-0.5 rounded bg-muted/20 text-muted border border-border"
              >
                {tag}
              </span>
            ))}
          </div>
        )}
      </CardContent>
    </Card>
    </div>
  )
}
