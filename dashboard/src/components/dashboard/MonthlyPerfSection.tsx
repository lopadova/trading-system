// MonthlyPerfSection — annual-by-month heatmap grid of returns. The active
// tab is "Monthly Returns"; the other four tabs are premium-locked placeholders.

import { useState } from 'react'
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { SegmentedControl } from '../ui/SegmentedControl'
import { useMonthlyReturns } from '../../hooks/useMonthlyReturns'
import type { AssetBucket } from '../../types/performance'
import { Lock } from 'lucide-react'

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec']

type Tab = 'monthly' | 'compounded' | 'cumulative' | 'drawdowns' | 'beta'

// Heatmap cell colour — scales from translucent green (positive) or red
// (negative) based on the absolute return, with a hard cap at 8%. Null/missing
// values use a muted grey, zero stays neutral.
function cellBg(v: number | null | undefined): string {
  if (v === null || v === undefined) return 'rgba(125,133,144,0.22)'
  if (v === 0) return 'rgba(125,133,144,0.14)'
  const abs = Math.min(Math.abs(v) / 8, 1)
  return v > 0
    ? `rgba(63,185,80,${0.18 + abs * 0.55})`
    : `rgba(248,81,73,${0.18 + abs * 0.55})`
}

function cellFg(v: number | null | undefined): string {
  if (v === null || v === undefined) return 'var(--fg-3)'
  if (v === 0) return 'var(--fg-2)'
  return '#ffffff'
}

export function MonthlyPerfSection({ asset }: { asset: AssetBucket }) {
  const [tab, setTab] = useState<Tab>('monthly')
  const { data, isLoading } = useMonthlyReturns(asset)

  // Sort years descending so the most recent appears at the top
  const years = data
    ? Object.keys(data.years)
        .map(Number)
        .sort((a, b) => b - a)
    : []

  return (
    <Card padding={0}>
      <div className="px-5 pt-4 pb-3.5 text-center border-b border-border">
        <h3 className="font-display text-[20px] font-bold m-0 mb-3.5">Monthly Performance</h3>
        <SegmentedControl<Tab>
          value={tab}
          onChange={setTab}
          options={[
            { value: 'monthly', label: 'Monthly Returns' },
            { value: 'compounded', label: 'Compounded Returns', locked: true },
            { value: 'cumulative', label: 'Cumulative Returns', locked: true },
            { value: 'drawdowns', label: 'Drawdowns', locked: true },
            { value: 'beta', label: 'Beta (12M)', locked: true },
          ]}
          size="sm"
        />
      </div>

      {tab !== 'monthly' ? (
        <div className="py-14 px-5 text-center text-muted text-[12.5px]">
          <Lock size={22} className="text-subtle inline-block" />
          <div className="mt-2">Premium view — locked</div>
        </div>
      ) : isLoading || !data ? (
        <div className="p-5">
          <Skeleton h={220} />
        </div>
      ) : (
        <div className="p-5 overflow-x-auto">
          <table
            className="w-full border-separate font-mono tabular-nums"
            style={{ borderSpacing: 2 }}
          >
            <thead>
              <tr>
                <th style={{ width: 46 }} />
                {MONTHS.map(m => (
                  <th
                    key={m}
                    className="text-[10.5px] text-muted font-medium uppercase tracking-wider pb-1.5 text-center"
                  >
                    {m}
                  </th>
                ))}
                <th className="text-[10.5px] text-muted font-semibold uppercase tracking-wider pb-1.5 text-center pl-2">
                  Total
                </th>
              </tr>
            </thead>
            <tbody>
              {years.map(yr => (
                <tr key={yr}>
                  <td className="text-[11px] text-muted font-medium pr-2 text-right">{yr}</td>
                  {data.years[String(yr)]!.map((v, i) => (
                    <td
                      key={i}
                      style={{ background: cellBg(v), color: cellFg(v) }}
                      className="text-[11px] font-semibold text-center py-2 rounded-sm min-w-[54px]"
                    >
                      {v === null || v === undefined ? '' : `${v.toFixed(2)}%`}
                    </td>
                  ))}
                  <td
                    style={{
                      background: cellBg(data.totals[String(yr)]),
                      color: cellFg(data.totals[String(yr)]),
                    }}
                    className="text-[11px] font-bold text-center py-2 px-1.5 rounded-sm"
                  >
                    {data.totals[String(yr)] !== undefined
                      ? `${data.totals[String(yr)]!.toFixed(2)}%`
                      : ''}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </Card>
  )
}
