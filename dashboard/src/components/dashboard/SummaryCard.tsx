// SummaryCard — Performance Summary grid (6 horizons × % or € unit toggle)
// Reads cached query data for the given asset bucket; the unit switch is
// purely local state.

import { useState } from 'react'
import { Card } from '../ui/Card'
import { SegmentedControl } from '../ui/SegmentedControl'
import { Skeleton } from '../ui/Skeleton'
import { usePerformanceSummary } from '../../hooks/usePerformanceSummary'
import { formatPercent, formatCurrency } from '../../utils/format'
import type { AssetBucket } from '../../types/performance'

type Unit = 'pct' | 'eur'

interface Row {
  k: string
  v: number
}

export function SummaryCard({ asset }: { asset: AssetBucket }) {
  const [unit, setUnit] = useState<Unit>('pct')
  const { data, isLoading } = usePerformanceSummary(asset)

  // Skeleton placeholder while the first payload lands — keeps layout stable
  if (isLoading || !data) {
    return (
      <Card>
        <Skeleton h={180} />
      </Card>
    )
  }

  // Six horizons, matching the worker's SummaryData shape
  const rows: Row[] = [
    { k: 'This Month', v: data.m },
    { k: 'Year To Date', v: data.ytd },
    { k: '2 Years', v: data.y2 },
    { k: '5 Years', v: data.y5 },
    { k: '10 Years', v: data.y10 },
    { k: 'Annualized', v: data.ann },
  ]

  // Render each row either as a signed percent, or as a signed EUR magnitude
  // computed off the base equity (data.base)
  const renderValue = (v: number): string => {
    if (unit === 'pct') return formatPercent(v, 2)
    const abs = Math.abs((data.base * v) / 100)
    const sign = v >= 0 ? '+' : '-'
    // formatCurrency emits its own sign for negative inputs; we pass a positive
    // magnitude and prefix the sign manually so both formats render the same way
    return `${sign}${formatCurrency(abs, 'EUR', 0)}`
  }

  return (
    <Card>
      <div className="flex items-start justify-between mb-4">
        <div>
          <h3 className="text-[15px] font-semibold m-0">Performance Summary</h3>
          <div className="text-[12px] text-muted mt-0.5">Returns across time horizons</div>
        </div>
        <SegmentedControl<Unit>
          value={unit}
          onChange={setUnit}
          options={[
            { value: 'pct', label: '%' },
            { value: 'eur', label: '€' },
          ]}
          size="sm"
        />
      </div>
      <div className="grid grid-cols-3 gap-0.5">
        {rows.map((r, i) => {
          const up = r.v >= 0
          // 3 columns x 2 rows grid — add dividers between cells
          const rightBorder = i % 3 !== 2 ? 'border-r' : ''
          const bottomBorder = i < 3 ? 'border-b' : ''
          return (
            <div
              key={r.k}
              className={`py-3.5 px-2 text-center ${rightBorder} ${bottomBorder} border-border`}
            >
              <div
                className={`font-mono text-[22px] font-semibold tabular-nums tracking-tight ${up ? 'text-up' : 'text-down'}`}
              >
                {renderValue(r.v)}
              </div>
              <div className="overline mt-1">{r.k}</div>
            </div>
          )
        })}
      </div>
    </Card>
  )
}
