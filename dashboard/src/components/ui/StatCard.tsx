import type { LucideIcon } from 'lucide-react'
import type { HTMLAttributes } from 'react'
import { Card } from './Card'
import { Badge, type BadgeTone } from './Badge'
import { cn } from '../../utils/cn'

export type DeltaTone = 'green' | 'red' | 'yellow' | 'muted'

export interface StatCardProps extends HTMLAttributes<HTMLDivElement> {
  label: string
  value: string
  delta?: string
  deltaTone?: DeltaTone
  icon?: LucideIcon
  status?: { tone: BadgeTone; label: string }
}

const deltaColor: Record<DeltaTone, string> = {
  green: 'text-up',
  red: 'text-down',
  yellow: 'text-[var(--yellow)]',
  muted: 'text-muted',
}

export function StatCard({ label, value, delta, deltaTone = 'muted', icon: Icon, status, className, ...rest }: StatCardProps) {
  return (
    <Card className={cn('cursor-default', className)} {...rest}>
      <div className="flex items-center justify-between mb-3">
        <span className="text-[12px] text-muted font-medium">{label}</span>
        {Icon && <Icon size={16} className="text-subtle" />}
      </div>
      {status ? (
        <div className="mb-2"><Badge tone={status.tone}>{status.label}</Badge></div>
      ) : (
        <div className="text-[26px] font-semibold text-[var(--fg-1)] mb-1.5 tabular-nums tracking-tight">{value}</div>
      )}
      {delta && (
        <div className={cn('text-[12px] font-medium', deltaColor[deltaTone])}>{delta}</div>
      )}
    </Card>
  )
}
