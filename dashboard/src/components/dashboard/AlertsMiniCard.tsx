// AlertsMiniCard — 24h alert counts summary tile; optional click target that
// navigates to the Alerts page.

import { Card } from '../ui/Card'
import { Badge } from '../ui/Badge'
import { Skeleton } from '../ui/Skeleton'
import { Bell } from 'lucide-react'
import { useAlertsSummary } from '../../hooks/useAlertsSummary'

interface Props {
  onClick?: () => void
}

export function AlertsMiniCard({ onClick }: Props) {
  const { data, isLoading } = useAlertsSummary()

  return (
    <Card className="cursor-pointer" onClick={onClick}>
      <div className="flex justify-between items-center mb-3">
        <div className="text-[12px] text-muted font-medium">Alerts · last 24h</div>
        <Bell size={16} className="text-subtle" />
      </div>
      {isLoading || !data ? (
        <Skeleton h={64} />
      ) : (
        <>
          <div className="text-[26px] font-semibold text-[var(--fg-1)] mb-2 tabular-nums tracking-tight">
            {data.total}
          </div>
          <div className="flex gap-1.5 flex-wrap">
            <Badge tone="red" size="sm">
              {data.critical} critical
            </Badge>
            <Badge tone="yellow" size="sm">
              {data.warning} warning
            </Badge>
            <Badge tone="blue" size="sm">
              {data.info} info
            </Badge>
          </div>
        </>
      )}
    </Card>
  )
}
