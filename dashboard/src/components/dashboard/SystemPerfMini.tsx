// SystemPerfMini — 4-panel system-metrics mini: CPU/RAM/Network rolling bars
// + disk usage gradient bar. Uses SystemMetricsSample payload.

import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Skeleton } from '../ui/Skeleton'
import { useSystemMetricsSample } from '../../hooks/useSystemMetrics'

interface MiniBarsProps {
  vals: number[]
  color: string
  label: string
}

function MiniBars({ vals, color, label }: MiniBarsProps) {
  // Use the newest reading as the headline value
  const last = vals.length > 0 ? vals[vals.length - 1]! : 0
  return (
    <div>
      <div className="flex justify-between text-[11px] mb-1">
        <span className="text-muted">{label}</span>
        <span className="font-mono font-medium">{last.toFixed(0)}%</span>
      </div>
      <div className="flex gap-[1.5px] items-end h-[26px]">
        {vals.map((v, i) => (
          <div
            key={i}
            className="flex-1 rounded-[1.5px]"
            style={{
              background: color,
              height: `${Math.max(2, v)}%`,
              // Fade older samples so the timeline is visually obvious
              opacity: 0.3 + (i / vals.length) * 0.7,
            }}
          />
        ))}
      </div>
    </div>
  )
}

export function SystemPerfMini() {
  const { data, isLoading } = useSystemMetricsSample()

  if (isLoading || !data) {
    return (
      <Card>
        <Skeleton h={170} />
      </Card>
    )
  }

  // Normalize the server timestamp to "YYYY-MM-DD HH:MM:SS" in UTC
  const asOf = new Date(data.asOf).toISOString().replace('T', ' ').slice(0, 19)
  const diskFreePct = 100 - data.diskUsedPct

  return (
    <Card>
      <div className="flex justify-between items-start mb-3">
        <div>
          <h3 className="m-0 text-[14px] font-semibold">System Performance</h3>
          <div className="text-[11px] text-muted mt-0.5 font-mono">as of {asOf} UTC</div>
        </div>
        <Badge tone="green" pulse size="sm">
          LIVE
        </Badge>
      </div>
      <div className="grid grid-cols-2 gap-3.5 gap-y-3">
        <MiniBars vals={data.cpu} color="#2f81f7" label="CPU" />
        <MiniBars vals={data.ram} color="#a371f7" label="RAM" />
        <MiniBars vals={data.network} color="#d29922" label="Network" />
        <div>
          <div className="flex justify-between text-[11px] mb-1">
            <span className="text-muted">Disk free</span>
            <span className="font-mono font-medium text-up">
              {data.diskFreeGb} GB · {diskFreePct}%
            </span>
          </div>
          <div className="h-2 bg-[var(--bg-1)] rounded overflow-hidden">
            <div
              className="h-full"
              style={{
                width: `${data.diskUsedPct}%`,
                // Gradient shifts green → yellow → red as disk fills up
                background:
                  'linear-gradient(to right,#3fb950 0%,#3fb950 70%,#d29922 85%,#f85149 100%)',
              }}
            />
          </div>
          <div className="text-[10px] text-subtle mt-1 font-mono">
            {data.diskUsedPct}% used of {data.diskTotalGb} GB
          </div>
        </div>
      </div>
    </Card>
  )
}
