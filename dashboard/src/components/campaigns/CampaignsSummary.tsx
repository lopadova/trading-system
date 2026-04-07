// Campaign summary statistics display
// Shows aggregate metrics for all filtered campaigns

import type { CampaignsResponse } from '../../types/campaign'
import { Card, CardHeader, CardTitle, CardContent } from '../ui/Card'
import { Activity, DollarSign, TrendingUp, Percent } from 'lucide-react'

interface CampaignsSummaryProps {
  data: CampaignsResponse
}

export function CampaignsSummary({ data }: CampaignsSummaryProps) {
  const { summary } = data

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      {/* Total Campaigns */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-sm font-medium text-muted">Campaigns</CardTitle>
            <Activity className="h-4 w-4 text-muted" />
          </div>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{summary.totalCampaigns}</div>
          <div className="text-xs text-muted mt-1">
            {summary.activeCampaigns} active, {summary.pausedCampaigns} paused,{' '}
            {summary.closedCampaigns} closed
          </div>
        </CardContent>
      </Card>

      {/* Total Capital */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-sm font-medium text-muted">Total Capital</CardTitle>
            <DollarSign className="h-4 w-4 text-muted" />
          </div>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">${summary.totalCapital.toLocaleString()}</div>
          <div className="text-xs text-muted mt-1">Across all campaigns</div>
        </CardContent>
      </Card>

      {/* Total P&L */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-sm font-medium text-muted">Total P&L</CardTitle>
            <TrendingUp className="h-4 w-4 text-muted" />
          </div>
        </CardHeader>
        <CardContent>
          <div
            className={`text-2xl font-bold ${
              summary.totalPnL >= 0 ? 'text-success' : 'text-danger'
            }`}
          >
            {summary.totalPnL >= 0 ? '+' : ''}${summary.totalPnL.toFixed(0)}
          </div>
          <div className="text-xs text-muted mt-1">
            {summary.totalPnLPct >= 0 ? '+' : ''}
            {summary.totalPnLPct.toFixed(2)}% overall
          </div>
        </CardContent>
      </Card>

      {/* Average Win Rate */}
      <Card>
        <CardHeader>
          <div className="flex items-center justify-between">
            <CardTitle className="text-sm font-medium text-muted">Avg Win Rate</CardTitle>
            <Percent className="h-4 w-4 text-muted" />
          </div>
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{(summary.avgWinRate * 100).toFixed(1)}%</div>
          <div className="text-xs text-muted mt-1">Across all campaigns</div>
        </CardContent>
      </Card>
    </div>
  )
}
