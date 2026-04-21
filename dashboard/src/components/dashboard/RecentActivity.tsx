// RecentActivity — feed of recent orders, signals, and system events. Each
// event picks its icon and colour tone from the payload's enum.

import { Card } from '../ui/Card'
import { Button } from '../ui/Button'
import { Skeleton } from '../ui/Skeleton'
import { useRecentActivity } from '../../hooks/useRecentActivity'
import {
  ExternalLink,
  CheckCircle2,
  AlertTriangle,
  Play,
  XCircle,
  Repeat,
  TrendingUp,
  RefreshCw,
  FileText,
  type LucideIcon,
} from 'lucide-react'
import type { ActivityIcon, ActivityTone } from '../../types/activity'
import { formatDistanceToNow } from 'date-fns'

// Map the payload icon slug to a Lucide component
const ICONS: Record<ActivityIcon, LucideIcon> = {
  'check-circle-2': CheckCircle2,
  'alert-triangle': AlertTriangle,
  play: Play,
  'x-circle': XCircle,
  repeat: Repeat,
  'trending-up': TrendingUp,
  'refresh-cw': RefreshCw,
  'file-text': FileText,
}

// Resolve payload tone to a CSS var for the icon colour
const toneColor: Record<ActivityTone, string> = {
  green: 'var(--green)',
  red: 'var(--red)',
  yellow: 'var(--yellow)',
  blue: 'var(--blue)',
  purple: 'var(--purple)',
  muted: 'var(--fg-2)',
}

export function RecentActivity() {
  const { data, isLoading } = useRecentActivity(8)

  return (
    <Card padding={0}>
      <div className="px-5 py-4 border-b border-border flex justify-between items-center">
        <div>
          <h3 className="m-0 text-[15px] font-semibold">Recent Activity</h3>
          <div className="text-[12px] text-muted mt-0.5">
            Orders, signals and system events · last 24h
          </div>
        </div>
        <Button variant="secondary" size="sm" icon={ExternalLink}>
          View all
        </Button>
      </div>
      {isLoading || !data ? (
        <div className="p-5">
          <Skeleton h={260} />
        </div>
      ) : (
        <div>
          {data.events.map((e, i) => {
            const IconCmp = ICONS[e.icon]
            const isLast = i === data.events.length - 1
            return (
              <div
                key={e.id}
                className={`px-5 py-2.5 flex items-center gap-3 transition-colors ${
                  !isLast ? 'border-b border-border' : ''
                } hover:bg-[color:var(--fg-1)]/[0.02]`}
              >
                <div className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center bg-[var(--bg-1)] border border-border">
                  <IconCmp size={14} color={toneColor[e.tone]} />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="text-[13px] font-medium text-[var(--fg-1)]">{e.title}</div>
                  <div className="text-[11.5px] text-muted truncate font-mono">{e.subtitle}</div>
                </div>
                <span className="text-[11px] text-muted whitespace-nowrap font-mono">
                  {formatDistanceToNow(new Date(e.timestamp), { addSuffix: true })}
                </span>
              </div>
            )
          })}
        </div>
      )}
    </Card>
  )
}
