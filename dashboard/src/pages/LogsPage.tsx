// LogsPage — live-stream log viewer.
// Ported from docs/design-kit/extracted/.../LogsPage.jsx (inline-styles → TSX +
// Tailwind). Keeps the mock log generator from the kit so filter/search/pause
// interactions still feel right; a future task will swap it for a Durable
// Object–backed tail via React Query.

import { useEffect, useMemo, useState } from 'react'
import { motion } from 'motion/react'
import {
  ChevronDown,
  ChevronRight,
  Search,
  SearchX,
  FileText,
  XCircle,
  AlertTriangle,
  Zap,
  Database,
  Play,
  Pause,
  Download,
} from 'lucide-react'
import { Card } from '../components/ui/Card'
import { StatCard } from '../components/ui/StatCard'
import { Badge } from '../components/ui/Badge'
import { Button } from '../components/ui/Button'
import { FilterOverlay } from '../components/ui/FilterOverlay'

// -----------------------------------------------------------------------------
// Level taxonomy — colour + tint for the level pill and expanded JSON block
// -----------------------------------------------------------------------------

type LogLevel = 'DEBUG' | 'INFO' | 'WARN' | 'ERROR' | 'TRACE'

interface LevelStyle {
  c: string
  bg: string
}

const LEVELS: Record<LogLevel, LevelStyle> = {
  DEBUG: { c: 'var(--fg-2)', bg: 'var(--tint-muted)' },
  INFO: { c: 'var(--blue)', bg: 'var(--tint-blue)' },
  WARN: { c: 'var(--yellow)', bg: 'var(--tint-yellow)' },
  ERROR: { c: 'var(--red)', bg: 'var(--tint-red)' },
  TRACE: { c: 'var(--purple)', bg: 'var(--tint-purple)' },
}

// -----------------------------------------------------------------------------
// Mock log generator — reproduces the kit's ten-base-templates rotation
// -----------------------------------------------------------------------------

interface LogEntry {
  id: string
  ts: string
  lvl: LogLevel
  src: string
  msg: string
  ctx: Record<string, unknown>
}

interface LogBase {
  lvl: LogLevel
  src: string
  msg: string
  ctx: Record<string, unknown>
}

const LOG_BASES: LogBase[] = [
  { lvl: 'INFO', src: 'OrderExecutor', msg: 'Order filled SPY 450C × 3 @ $12.40 (slip $0.02)', ctx: { orderId: 'ord_8f2a', strategy: 'IronCondor_SPY_0DTE' } },
  { lvl: 'DEBUG', src: 'GreeksMonitorWorker', msg: 'Recomputed portfolio greeks · delta=+42.3 theta=-58.2 vega=+124.5', ctx: { durationMs: 14 } },
  { lvl: 'WARN', src: 'IVTSMonitor', msg: 'QQQ 30-day IV rank 87 exceeds warning threshold (70)', ctx: { symbol: 'QQQ', iv30: 24.2, rank: 87 } },
  { lvl: 'INFO', src: 'IBKRBridge', msg: 'Market data subscription established · 42 contracts', ctx: { latencyMs: 38 } },
  { lvl: 'ERROR', src: 'NotificationDispatcher', msg: 'Failed to deliver webhook (503 Service Unavailable)', ctx: { channel: 'discord', retryIn: '60s' } },
  { lvl: 'INFO', src: 'CampaignsEngine', msg: "Campaign 'QQQ Weekly Put Spread' evaluated · 0 new orders", ctx: { campaignId: 'cmp_7c1e' } },
  { lvl: 'DEBUG', src: 'CalendarWorker', msg: 'Next economic event: FOMC meeting in 2h 14m', ctx: { severity: 'high' } },
  { lvl: 'TRACE', src: 'OrderQueue', msg: 'Enqueued order ord_9b44 (priority=0, queue_depth=128)', ctx: {} },
  { lvl: 'WARN', src: 'MarketDataFeed', msg: 'Backup feed degraded · failing over to primary', ctx: { latencyMs: 180 } },
  { lvl: 'INFO', src: 'TradingSupervisor', msg: 'Heartbeat · 9/10 services healthy', ctx: { degraded: ['NotificationDispatcher'] } },
]

function makeLog(i: number): LogEntry {
  // Wrap index into the base pool; stagger timestamps so the list doesn't look
  // like a batch dump
  const b = LOG_BASES[i % LOG_BASES.length]!
  const secs = i * 3 + (i % 7)
  const ts = new Date(Date.now() - secs * 1000)
  const hh = String(ts.getHours()).padStart(2, '0')
  const mm = String(ts.getMinutes()).padStart(2, '0')
  const ss = String(ts.getSeconds()).padStart(2, '0')
  const ms = String(ts.getMilliseconds()).padStart(3, '0')
  return { id: `log_${i}`, ts: `${hh}:${mm}:${ss}.${ms}`, ...b }
}

// -----------------------------------------------------------------------------
// Filters
// -----------------------------------------------------------------------------

type LevelFilter = 'All' | 'Debug' | 'Info' | 'Warn' | 'Error' | 'Trace'

const LEVEL_CHIPS: LevelFilter[] = [
  'All',
  'Debug',
  'Info',
  'Warn',
  'Error',
  'Trace',
]

const SOURCES = [
  'All',
  'OrderExecutor',
  'GreeksMonitorWorker',
  'IVTSMonitor',
  'IBKRBridge',
  'NotificationDispatcher',
  'CampaignsEngine',
  'CalendarWorker',
  'OrderQueue',
  'MarketDataFeed',
  'TradingSupervisor',
]

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
// LogRow — one table row, toggles to expand a JSON payload below
// -----------------------------------------------------------------------------

interface LogRowProps {
  log: LogEntry
  expanded: boolean
  onToggle: () => void
}

function LogRow({ log, expanded, onToggle }: LogRowProps) {
  const lvl = LEVELS[log.lvl]
  return (
    <>
      <tr
        onClick={onToggle}
        className="border-b border-[color:var(--border-1)]/50 cursor-pointer hover:bg-[color:var(--fg-2)]/5 transition-colors"
      >
        <td className="px-3 py-1.5 font-mono text-[11.5px] text-muted whitespace-nowrap align-top">
          {log.ts}
        </td>
        <td className="px-2.5 py-1.5 align-top whitespace-nowrap">
          <span
            className="px-[7px] py-px rounded-sm text-[10.5px] font-semibold tracking-wider font-mono"
            style={{ background: lvl.bg, color: lvl.c }}
          >
            {log.lvl}
          </span>
        </td>
        <td className="px-2.5 py-1.5 font-mono text-[11.5px] text-[var(--purple)] whitespace-nowrap align-top">
          {log.src}
        </td>
        <td className="px-3 py-1.5 font-mono text-[12px] text-[var(--fg-1)] align-top break-words">
          {log.msg}
        </td>
        <td className="px-2.5 py-1.5 align-top text-subtle text-[11px]">
          {expanded ? <ChevronDown size={12} /> : <ChevronRight size={12} />}
        </td>
      </tr>
      {expanded && (
        <tr style={{ background: 'rgba(47,129,247,0.03)' }}>
          <td
            colSpan={5}
            className="py-2.5 pr-4 pl-[84px] border-b border-[color:var(--border-1)]/50"
          >
            <pre className="m-0 font-mono text-[11.5px] text-muted whitespace-pre-wrap leading-[1.55]">
              {JSON.stringify({ id: log.id, ...log.ctx }, null, 2)}
            </pre>
          </td>
        </tr>
      )}
    </>
  )
}

// -----------------------------------------------------------------------------
// LogsPage — main composition
// -----------------------------------------------------------------------------

export function LogsPage() {
  const [level, setLevel] = useState<LevelFilter>('All')
  const [source, setSource] = useState<string>('All')
  const [query, setQuery] = useState('')
  const [expanded, setExpanded] = useState<string | null>(null)
  const [loading, setLoading] = useState(false)
  const [paused, setPaused] = useState(false)
  const [logs, setLogs] = useState<LogEntry[]>(() =>
    Array.from({ length: 60 }, (_, i) => makeLog(i)),
  )

  // Live stream — every 2.2s prepend a new log and cap at 200 entries. The
  // timer is cleared when the user pauses or unmounts.
  useEffect(() => {
    if (paused) return
    const t = setInterval(() => {
      setLogs(curr => [makeLog(Date.now() % 10000), ...curr].slice(0, 200))
    }, 2200)
    return () => clearInterval(t)
  }, [paused])

  // Wrap a state mutation in a short fake network pause so the FilterOverlay
  // has something to animate during filter switches
  const triggerLoad = (fn: () => void) => {
    setLoading(true)
    setTimeout(() => {
      fn()
      setLoading(false)
    }, 420)
  }

  // Multi-predicate client-side filter
  const filtered = useMemo(() => {
    return logs.filter(l => {
      const levelOk =
        level === 'All' || l.lvl === (level.toUpperCase() as LogLevel)
      if (!levelOk) return false
      if (source !== 'All' && l.src !== source) return false
      if (query !== '') {
        const hay = `${l.msg} ${l.src}`.toLowerCase()
        if (!hay.includes(query.toLowerCase())) return false
      }
      return true
    })
  }, [logs, level, source, query])

  return (
    <div className="p-8 flex flex-col gap-4">
      {/* Row 0 — KPI strip */}
      <motion.div
        {...stagger(0)}
        className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4"
      >
        <StatCard
          label="Total · 1h"
          value={logs.length.toLocaleString()}
          icon={FileText}
          delta="live stream"
          deltaTone="muted"
        />
        <StatCard
          label="Errors"
          value="3"
          icon={XCircle}
          delta="last 1h"
          deltaTone="red"
        />
        <StatCard
          label="Warnings"
          value="12"
          icon={AlertTriangle}
          delta="last 1h"
          deltaTone="yellow"
        />
        <StatCard
          label="Throughput"
          value="184/m"
          icon={Zap}
          delta="↑ 12/m vs avg"
          deltaTone="green"
        />
        <StatCard
          label="Retention"
          value="7 days"
          icon={Database}
          delta="rolling · compressed"
          deltaTone="muted"
        />
      </motion.div>

      {/* Row 1 — filter bar + log stream */}
      <motion.div {...stagger(1)}>
        <Card padding={0}>
          <div className="px-4 py-3 border-b border-border flex gap-2.5 items-center flex-wrap">
            <div className="relative flex-1 min-w-[240px] max-w-[360px]">
              <Search
                size={13}
                className="absolute left-2.5 top-2.5 text-muted pointer-events-none"
              />
              <input
                type="text"
                value={query}
                onChange={e => setQuery(e.target.value)}
                placeholder="Search messages, sources…"
                className="w-full py-1.5 pl-8 pr-2.5 bg-[var(--bg-1)] border border-border rounded-md text-[var(--fg-1)] text-[12.5px] box-border outline-none focus:border-[var(--blue)]"
              />
            </div>

            <div className="flex gap-1 p-[3px] bg-[var(--bg-1)] border border-border rounded-md">
              {LEVEL_CHIPS.map(l => {
                const on = level === l
                return (
                  <button
                    key={l}
                    type="button"
                    onClick={() => triggerLoad(() => setLevel(l))}
                    className={`px-2.5 py-1 rounded text-[11.5px] border-none ${
                      on
                        ? 'bg-[var(--bg-3)] text-[var(--fg-1)] font-medium'
                        : 'bg-transparent text-muted'
                    }`}
                  >
                    {l}
                  </button>
                )
              })}
            </div>

            <select
              value={source}
              onChange={e => triggerLoad(() => setSource(e.target.value))}
              className="px-2.5 py-1.5 bg-[var(--bg-1)] border border-border rounded-md text-[var(--fg-1)] text-[12.5px] outline-none"
            >
              {SOURCES.map(s => (
                <option key={s} value={s}>
                  {s === 'All' ? 'All sources' : s}
                </option>
              ))}
            </select>

            <div className="ml-auto flex gap-1.5 items-center">
              <Badge tone={paused ? 'muted' : 'green'} pulse={!paused}>
                {paused ? 'PAUSED' : 'LIVE'}
              </Badge>
              <Button
                variant="secondary"
                size="sm"
                icon={paused ? Play : Pause}
                onClick={() => setPaused(p => !p)}
              >
                {paused ? 'Resume' : 'Pause'}
              </Button>
              <Button variant="secondary" size="sm" icon={Download}>
                Export
              </Button>
            </div>
          </div>

          <div className="relative min-h-[280px]">
            <FilterOverlay visible={loading} label="Loading logs…" />
            <div className="max-h-[520px] overflow-auto">
              <table className="w-full border-collapse table-fixed">
                <colgroup>
                  <col style={{ width: 110 }} />
                  <col style={{ width: 66 }} />
                  <col style={{ width: 180 }} />
                  <col />
                  <col style={{ width: 30 }} />
                </colgroup>
                <thead>
                  <tr className="bg-[var(--bg-1)] sticky top-0 z-[1]">
                    {['Timestamp', 'Level', 'Source', 'Message', ''].map(
                      (h, i) => (
                        <th
                          key={i}
                          className="px-3 py-2 text-left text-[10.5px] font-medium text-muted uppercase tracking-wider border-b border-border"
                        >
                          {h}
                        </th>
                      ),
                    )}
                  </tr>
                </thead>
                <tbody>
                  {filtered.slice(0, 80).map(l => (
                    <LogRow
                      key={l.id}
                      log={l}
                      expanded={expanded === l.id}
                      onToggle={() =>
                        setExpanded(e => (e === l.id ? null : l.id))
                      }
                    />
                  ))}
                </tbody>
              </table>
              {filtered.length === 0 && !loading && (
                <div className="text-center py-12 text-muted">
                  <SearchX size={28} className="text-subtle inline-block" />
                  <div className="mt-3 text-[13px] font-medium text-[var(--fg-1)]">
                    No logs match
                  </div>
                  <div className="mt-1 text-[12px]">
                    Try widening the level or clearing the search.
                  </div>
                </div>
              )}
            </div>
          </div>

          <div className="px-4 py-2 border-t border-border flex justify-between text-[11px] text-muted font-mono">
            <span>
              Showing {Math.min(80, filtered.length)} of {filtered.length} ·
              buffer {logs.length}/200
            </span>
            <span>UTC · tail -f · press ⎵ to pause</span>
          </div>
        </Card>
      </motion.div>
    </div>
  )
}
