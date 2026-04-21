// DrawdownsSection — drawdown chart (Portfolio vs S&P 500) on the left, and
// a list of the worst recorded drawdowns on the right. Ranges are Max / 10Y /
// 5Y / 1Y / YTD / 6M and drive the worker query.

import { useState } from 'react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ResponsiveContainer,
  CartesianGrid,
} from 'recharts'
import { Card } from '../ui/Card'
import { Skeleton } from '../ui/Skeleton'
import { SegmentedControl } from '../ui/SegmentedControl'
import { useDrawdowns } from '../../hooks/useDrawdowns'
import type { AssetBucket } from '../../types/performance'
import type { DrawdownRange } from '../../types/drawdown'
import { formatPercent } from '../../utils/format'

const RANGES: DrawdownRange[] = ['Max', '10Y', '5Y', '1Y', 'YTD', '6M']

export function DrawdownsSection({ asset }: { asset: AssetBucket }) {
  const [range, setRange] = useState<DrawdownRange>('10Y')
  const { data, isLoading } = useDrawdowns(asset, range)

  return (
    <div className="grid grid-cols-1 lg:grid-cols-[2fr_1fr] gap-4">
      <Card>
        <div className="flex justify-between items-center mb-2.5">
          <h3 className="text-[15px] font-semibold m-0">Drawdowns</h3>
          <SegmentedControl<DrawdownRange>
            value={range}
            onChange={setRange}
            options={RANGES.map(r => ({ value: r, label: r }))}
            size="sm"
          />
        </div>
        <div className="flex gap-4 mb-2 text-[11.5px] text-muted">
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2 h-2 rounded-full bg-[var(--green)]" />
            Portfolio
          </span>
          <span className="inline-flex items-center gap-1.5">
            <span className="w-2 h-2 rounded-full bg-[var(--blue)]" />
            S&amp;P 500
          </span>
        </div>
        <div className="h-[220px]">
          {isLoading || !data ? (
            <Skeleton h={220} />
          ) : (
            <ResponsiveContainer width="100%" height="100%">
              <LineChart
                data={data.portfolioSeries.map((v, i) => ({
                  i,
                  portfolio: v,
                  sp500: data.sp500Series[i],
                }))}
                margin={{ top: 14, right: 12, bottom: 10, left: 0 }}
              >
                <CartesianGrid stroke="var(--border-2)" vertical={false} />
                <XAxis
                  dataKey="i"
                  stroke="var(--fg-2)"
                  fontSize={10}
                  tickLine={false}
                  axisLine={{ stroke: 'var(--border-1)' }}
                />
                <YAxis
                  stroke="var(--fg-2)"
                  fontSize={10}
                  tickLine={false}
                  axisLine={{ stroke: 'var(--border-1)' }}
                  tickFormatter={(v: number) => `${v.toFixed(0)}%`}
                />
                <Tooltip
                  contentStyle={{
                    background: 'var(--bg-2)',
                    border: '1px solid var(--border-1)',
                    borderRadius: 8,
                  }}
                />
                <Line
                  type="monotone"
                  dataKey="sp500"
                  stroke="#2f81f7"
                  strokeWidth={1.5}
                  dot={false}
                  isAnimationActive={false}
                />
                <Line
                  type="monotone"
                  dataKey="portfolio"
                  stroke="#3fb950"
                  strokeWidth={1.8}
                  dot={false}
                  isAnimationActive={false}
                />
              </LineChart>
            </ResponsiveContainer>
          )}
        </div>
      </Card>

      <Card padding={0}>
        <div className="px-4 py-3.5 border-b border-border">
          <h3 className="text-[14px] font-semibold m-0">Worst Drawdowns</h3>
        </div>
        <table className="w-full border-collapse">
          <thead>
            <tr className="bg-[var(--bg-1)]">
              {['Depth', 'Start', 'End', 'Months'].map(h => (
                <th
                  key={h}
                  className="px-3 py-2 text-left text-[10.5px] font-medium overline border-b border-border"
                >
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data?.worst.map((d, i) => (
              <tr key={i} className="border-b border-border last:border-0">
                <td className="px-3 py-2.5 font-mono text-down font-medium text-[12.5px]">
                  {formatPercent(d.depthPct)}
                </td>
                <td className="px-3 py-2.5 font-mono text-[12.5px]">{d.start}</td>
                <td className="px-3 py-2.5 font-mono text-[12.5px]">{d.end}</td>
                <td className="px-3 py-2.5 font-mono text-muted text-[12.5px]">
                  {d.months}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </Card>
    </div>
  )
}
