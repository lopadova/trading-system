// HealthPage — System Health view.
// Ported from docs/design-kit/extracted/.../SystemHealthPage.jsx (inline-styles
// → TSX + Tailwind). Uses local mock data for now; a future task will wire it
// to the real /api/metrics/services + /api/metrics/host endpoints.

import { useState } from 'react'
import { motion } from 'motion/react'
import {
  Server,
  Zap,
  Layers,
  Plug,
  Cpu,
  Radio,
  CheckCircle2,
  AlertTriangle,
  RefreshCw,
  PowerOff,
  Upload,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { Card } from '../components/ui/Card'
import { StatCard } from '../components/ui/StatCard'
import { Badge, type BadgeTone } from '../components/ui/Badge'
import { FilterOverlay } from '../components/ui/FilterOverlay'

// Staggered fade-in-up — same easing as HomePage
function stagger(i: number) {
  return {
    initial: { opacity: 0, y: 8 },
    animate: { opacity: 1, y: 0 },
    transition: {
      duration: 0.28,
      delay: i * 0.04,
      ease: [0.4, 0, 0.2, 1] as const,
    },
  }
}

// -----------------------------------------------------------------------------
// HealthBar — labelled progress bar for a single resource (CPU, RAM, ...)
// -----------------------------------------------------------------------------

type HealthBarTone = 'green' | 'red' | 'yellow' | 'blue'

interface HealthBarProps {
  label: string
  value: number
  unit?: string
  tone?: HealthBarTone
  rightLabel?: string
}

const HEALTH_BAR_COLOR: Record<HealthBarTone, string> = {
  green: 'var(--green)',
  red: 'var(--red)',
  yellow: 'var(--yellow)',
  blue: 'var(--blue)',
}

function HealthBar({
  label,
  value,
  unit = '%',
  tone = 'blue',
  rightLabel,
}: HealthBarProps) {
  const color = HEALTH_BAR_COLOR[tone]
  return (
    <div className="py-2.5 border-b border-border last:border-0">
      <div className="flex justify-between items-baseline mb-1.5">
        <span className="text-[12.5px] text-[var(--fg-1)] font-medium">
          {label}
        </span>
        <span
          className="font-mono text-[12.5px] tabular-nums"
          style={{ color }}
        >
          {value}
          {unit}
          {rightLabel ? ` · ${rightLabel}` : ''}
        </span>
      </div>
      <div className="h-1.5 bg-[var(--bg-1)] rounded-[3px] overflow-hidden">
        <div
          className="h-full rounded-[3px] transition-[width] duration-[600ms] ease-[cubic-bezier(0.4,0,0.2,1)]"
          style={{
            width: `${Math.min(100, value)}%`,
            background: color,
          }}
        />
      </div>
    </div>
  )
}

// -----------------------------------------------------------------------------
// ServiceRow — one entry in the Services & Workers table
// -----------------------------------------------------------------------------

type ServiceKind = 'worker' | 'bridge' | 'queue' | 'feed' | 'service'
type ServiceStatus = 'RUNNING' | 'DEGRADED' | 'STOPPED'

interface ServiceEntry {
  name: string
  kind: ServiceKind
  status: ServiceStatus
  uptime: string
  lastHeartbeat: string
  latency: number
}

const KIND_ICON: Record<ServiceKind, LucideIcon> = {
  worker: Cpu,
  bridge: Plug,
  queue: Layers,
  feed: Radio,
  service: Server,
}

const STATUS_TONE: Record<ServiceStatus, BadgeTone> = {
  RUNNING: 'green',
  DEGRADED: 'yellow',
  STOPPED: 'muted',
}

// Pick a latency colour based on thresholds from the kit
function latencyColor(latency: number): string {
  if (latency > 150) return 'text-[var(--red)]'
  if (latency > 50) return 'text-[var(--yellow)]'
  return 'text-[var(--green)]'
}

function ServiceRow({ s }: { s: ServiceEntry }) {
  const Icon = KIND_ICON[s.kind]
  return (
    <tr className="border-b border-border last:border-0">
      <td className="px-4 py-[11px]">
        <div className="flex items-center gap-2.5">
          <Icon size={14} className="text-subtle" />
          <div>
            <div className="text-[13px] font-medium text-[var(--fg-1)]">
              {s.name}
            </div>
            <div className="text-[11px] text-muted font-mono mt-px capitalize">
              {s.kind}
            </div>
          </div>
        </div>
      </td>
      <td className="px-4 py-[11px]">
        <Badge tone={STATUS_TONE[s.status]} pulse={s.status === 'RUNNING'}>
          {s.status}
        </Badge>
      </td>
      <td className="px-4 py-[11px] font-mono text-[12.5px] tabular-nums text-muted">
        {s.uptime}
      </td>
      <td className="px-4 py-[11px] font-mono text-[12px] text-muted">
        {s.lastHeartbeat}
      </td>
      <td
        className={`px-4 py-[11px] text-right font-mono text-[12.5px] tabular-nums ${latencyColor(s.latency)}`}
      >
        {s.latency}ms
      </td>
    </tr>
  )
}

// -----------------------------------------------------------------------------
// Filter chips used above the services table
// -----------------------------------------------------------------------------

type ServiceFilter = 'All' | 'Running' | 'Issues' | 'Workers'

function filterServices(
  services: ServiceEntry[],
  f: ServiceFilter,
): ServiceEntry[] {
  if (f === 'All') return services
  if (f === 'Running') return services.filter(s => s.status === 'RUNNING')
  if (f === 'Issues') return services.filter(s => s.status !== 'RUNNING')
  return services.filter(s => s.kind === 'worker')
}

// -----------------------------------------------------------------------------
// Main page
// -----------------------------------------------------------------------------

// Mock data — matches the design kit verbatim. Real data source comes later.
const SERVICES: ServiceEntry[] = [
  { name: 'TradingSupervisorService', kind: 'service', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '1s ago', latency: 14 },
  { name: 'OptionsExecutionService', kind: 'service', status: 'RUNNING', uptime: '4d 12h 05m', lastHeartbeat: '1s ago', latency: 22 },
  { name: 'IBKRBridge', kind: 'bridge', status: 'RUNNING', uptime: '2d 04h 11m', lastHeartbeat: '1s ago', latency: 38 },
  { name: 'GreeksMonitorWorker', kind: 'worker', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '2s ago', latency: 11 },
  { name: 'IVTSMonitor', kind: 'worker', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '3s ago', latency: 9 },
  { name: 'CalendarWorker', kind: 'worker', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '4s ago', latency: 6 },
  { name: 'MarketDataFeed · primary', kind: 'feed', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '<1s ago', latency: 42 },
  { name: 'MarketDataFeed · backup', kind: 'feed', status: 'DEGRADED', uptime: '0d 08h 22m', lastHeartbeat: '12s ago', latency: 180 },
  { name: 'OrderQueue', kind: 'queue', status: 'RUNNING', uptime: '4d 12h 08m', lastHeartbeat: '1s ago', latency: 4 },
  { name: 'NotificationDispatcher', kind: 'worker', status: 'STOPPED', uptime: '—', lastHeartbeat: '6h ago', latency: 0 },
]

interface RecentEvent {
  icon: LucideIcon
  color: string
  text: string
  when: string
}

const RECENT_EVENTS: RecentEvent[] = [
  { icon: CheckCircle2, color: 'var(--green)', text: 'All systems nominal', when: '18s ago' },
  { icon: AlertTriangle, color: 'var(--yellow)', text: 'Backup feed degraded', when: '12m ago' },
  { icon: RefreshCw, color: 'var(--blue)', text: 'IBKRBridge reconnected', when: '28m ago' },
  { icon: PowerOff, color: 'var(--red)', text: 'NotificationDispatcher stopped', when: '6h ago' },
  { icon: Upload, color: 'var(--fg-2)', text: 'Deploy v0.1.0 completed', when: '4d ago' },
]

export function HealthPage() {
  const [filter, setFilter] = useState<ServiceFilter>('All')
  const [loading, setLoading] = useState(false)

  // Simulate a short network pause on filter change so the FilterOverlay has
  // something to animate — real implementation will use isFetching from the
  // React Query hook
  const pickFilter = (f: ServiceFilter) => {
    if (f === filter) return
    setLoading(true)
    setTimeout(() => {
      setFilter(f)
      setLoading(false)
    }, 440)
  }

  const visible = filterServices(SERVICES, filter)

  return (
    <div className="p-8 flex flex-col gap-4">
      {/* Row 0 — KPI strip */}
      <motion.div
        {...stagger(0)}
        className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4"
      >
        <StatCard
          label="Services Up"
          value="9 / 10"
          icon={Server}
          delta="1 stopped · 1 degraded"
          deltaTone="yellow"
        />
        <StatCard
          label="Avg Latency"
          value="14ms"
          icon={Zap}
          delta="↓ 3ms vs 1h avg"
          deltaTone="green"
        />
        <StatCard
          label="Queue Depth"
          value="128"
          icon={Layers}
          delta="order queue · nominal"
          deltaTone="muted"
        />
        <StatCard
          label="IBKR Session"
          value="—"
          icon={Plug}
          status={{ tone: 'green', label: 'CONNECTED' }}
          delta="Paper · US equities · opts"
          deltaTone="green"
        />
      </motion.div>

      {/* Row 1 — three resource panels */}
      <motion.div
        {...stagger(1)}
        className="grid grid-cols-1 lg:grid-cols-3 gap-4"
      >
        <Card>
          <div className="flex justify-between items-center mb-1.5">
            <h3 className="m-0 text-[14px] font-semibold">Resource Usage</h3>
            <span className="text-[11px] text-muted font-mono">
              host · supervisor-01
            </span>
          </div>
          <HealthBar label="CPU" value={34} tone="green" />
          <HealthBar
            label="Memory"
            value={58}
            tone="blue"
            rightLabel="3.2 / 5.5 GB"
          />
          <HealthBar
            label="Disk"
            value={21}
            tone="green"
            rightLabel="42 / 200 GB"
          />
          <HealthBar
            label="Network I/O"
            value={71}
            tone="yellow"
            rightLabel="14.2 MB/s"
          />
        </Card>

        <Card>
          <div className="flex justify-between items-center mb-1.5">
            <h3 className="m-0 text-[14px] font-semibold">Rate Limits</h3>
            <span className="text-[11px] text-muted font-mono">last 60s</span>
          </div>
          <HealthBar
            label="IBKR requests"
            value={48}
            tone="green"
            rightLabel="480 / 1000"
          />
          <HealthBar
            label="Market-data quotes"
            value={82}
            tone="yellow"
            rightLabel="8.2k / 10k"
          />
          <HealthBar
            label="Order submissions"
            value={12}
            tone="green"
            rightLabel="12 / 100"
          />
          <HealthBar
            label="WebSocket frames"
            value={64}
            tone="blue"
            rightLabel="64k / 100k"
          />
        </Card>

        <Card>
          <h3 className="m-0 text-[14px] font-semibold mb-2.5">
            Recent Events
          </h3>
          {RECENT_EVENTS.map((e, i) => (
            <div
              key={i}
              className={`flex items-start gap-2.5 py-[7px] text-[12.5px] ${
                i < RECENT_EVENTS.length - 1 ? 'border-b border-border' : ''
              }`}
            >
              <e.icon size={14} style={{ color: e.color, marginTop: 2 }} />
              <div className="flex-1">{e.text}</div>
              <span className="text-[11px] text-muted font-mono whitespace-nowrap">
                {e.when}
              </span>
            </div>
          ))}
        </Card>
      </motion.div>

      {/* Row 2 — services & workers table */}
      <motion.div {...stagger(2)}>
        <Card padding={0}>
          <div className="flex justify-between items-center px-5 py-3.5 border-b border-border">
            <div>
              <h3 className="m-0 text-[15px] font-semibold">
                Services &amp; Workers
              </h3>
              <div className="text-[12px] text-muted mt-0.5">
                {SERVICES.length} total · last refresh 3s ago
              </div>
            </div>
            <div className="flex gap-1.5">
              {(['All', 'Running', 'Issues', 'Workers'] as ServiceFilter[]).map(
                f => {
                  const on = filter === f
                  return (
                    <button
                      key={f}
                      type="button"
                      onClick={() => pickFilter(f)}
                      className={`px-3 py-[5px] rounded-md text-[12px] border border-border transition-colors ${
                        on
                          ? 'bg-[var(--bg-3)] text-[var(--fg-1)] font-medium'
                          : 'bg-transparent text-muted'
                      }`}
                    >
                      {f}
                    </button>
                  )
                },
              )}
            </div>
          </div>
          <div className="relative min-h-[200px]">
            <FilterOverlay
              visible={loading}
              label={`Loading ${filter.toLowerCase()} services…`}
            />
            <table className="w-full border-collapse">
              <thead>
                <tr className="bg-[var(--bg-1)]">
                  {['Service', 'Status', 'Uptime', 'Last heartbeat', 'Latency'].map(
                    (h, i) => (
                      <th
                        key={h}
                        className={`px-4 py-2.5 text-[11px] font-medium text-muted uppercase tracking-wider border-b border-border ${
                          i === 4 ? 'text-right' : 'text-left'
                        }`}
                      >
                        {h}
                      </th>
                    ),
                  )}
                </tr>
              </thead>
              <tbody>
                {visible.map(s => (
                  <ServiceRow key={s.name} s={s} />
                ))}
              </tbody>
            </table>
          </div>
        </Card>
      </motion.div>
    </div>
  )
}
