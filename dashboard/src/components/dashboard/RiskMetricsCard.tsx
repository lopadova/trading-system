// RiskMetricsCard — compact risk snapshot (greek exposures + market risk
// indicators + buying power + margin). Rows are colour-coded by tone.

import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { useRiskMetrics } from '../../hooks/useRiskMetrics'
import { formatCurrency, formatPercent } from '../../utils/format'

type RowTone = 'green' | 'red' | 'yellow' | 'muted'

interface Row {
  k: string
  v: string
  tone: RowTone
}

export function RiskMetricsCard() {
  const { data, isLoading } = useRiskMetrics()

  if (isLoading || !data) {
    return (
      <Card>
        <Skeleton h={260} />
      </Card>
    )
  }

  // Map raw metrics to display rows with tone resolution
  const rows: Row[] = [
    { k: 'Portfolio Delta', v: formatPercent(data.delta), tone: data.delta >= 0 ? 'green' : 'red' },
    { k: 'Portfolio Theta', v: data.theta.toFixed(2), tone: data.theta >= 0 ? 'green' : 'red' },
    { k: 'Portfolio Vega', v: formatPercent(data.vega), tone: 'green' },
    { k: 'VIX Index', v: data.vix !== null ? data.vix.toFixed(2) : '—', tone: 'muted' },
    { k: 'VIX1D', v: data.vix1d !== null ? data.vix1d.toFixed(2) : '—', tone: 'muted' },
    {
      k: 'IV Rank (SPY)',
      v: data.ivRankSpy !== null ? `${(data.ivRankSpy * 100).toFixed(0)}%` : '—',
      tone: 'muted',
    },
    { k: 'Buying Power', v: formatCurrency(data.buyingPower, 'USD', 0), tone: 'muted' },
    {
      k: 'Margin Used',
      v: `${data.marginUsedPct}%`,
      tone: data.marginUsedPct > 70 ? 'red' : data.marginUsedPct > 55 ? 'yellow' : 'muted',
    },
  ]

  return (
    <Card>
      <h3 className="m-0 text-[15px] font-semibold mb-3">Risk Metrics</h3>
      {rows.map(r => (
        <div
          key={r.k}
          className="flex justify-between items-center py-[7px] border-b border-border last:border-0 text-[12.5px]"
        >
          <span className="text-muted">{r.k}</span>
          <span
            className={`font-mono tabular-nums font-medium ${
              r.tone === 'green'
                ? 'text-up'
                : r.tone === 'red'
                  ? 'text-down'
                  : r.tone === 'yellow'
                    ? 'text-[var(--yellow)]'
                    : ''
            }`}
          >
            {r.v}
          </span>
        </div>
      ))}
    </Card>
  )
}
