import { Bell, Sun, Moon } from 'lucide-react'
import { Badge } from '../ui/Badge'
import { useThemeStore } from '../../stores/themeStore'
import { useAlertsSummary } from '../../hooks/useAlertsSummary'

interface HeaderProps {
  title: string
  subtitle?: string
}

export function Header({ title, subtitle }: HeaderProps) {
  const theme = useThemeStore(s => s.theme)
  const toggle = useThemeStore(s => s.toggle)
  // Show a red pulse dot on the bell when there are unacked critical alerts
  // in the last 24h. This piggy-backs on the same query the Overview page
  // uses, so it's free from a network-traffic standpoint.
  const { data: alerts } = useAlertsSummary()
  const hasCritical = (alerts?.critical ?? 0) > 0
  return (
    <header className="h-16 border-b border-border bg-[var(--bg-1)] flex items-center justify-between px-8 shrink-0">
      <div>
        <h1 className="font-display text-[20px] font-semibold text-[var(--fg-1)] m-0 tracking-tight">{title}</h1>
        {subtitle && <div className="text-[12px] text-muted mt-0.5">{subtitle}</div>}
      </div>
      <div className="flex items-center gap-3">
        <Badge tone="green" pulse>OPERATIONAL</Badge>
        <div className="w-px h-6 bg-border" />
        {/* Anchor (not button) so the App-level click handler intercepts and
            navigates via history.pushState, matching every other nav link. */}
        <a
          href="/alerts"
          aria-label={
            hasCritical
              ? `Open alerts (${alerts?.critical} critical)`
              : 'Open alerts'
          }
          className="relative w-8 h-8 rounded-md border border-border flex items-center justify-center text-muted hover:text-[var(--fg-1)] transition-colors"
        >
          <Bell size={15} />
          {hasCritical && (
            <span
              aria-hidden="true"
              className="absolute top-1 right-1 w-2 h-2 rounded-full bg-[var(--red)] pulse-dot"
              style={{ boxShadow: '0 0 4px var(--red)' }}
            />
          )}
        </a>
        <button
          type="button"
          aria-label={theme === 'dark' ? 'Switch to light theme' : 'Switch to dark theme'}
          onClick={toggle}
          className="w-8 h-8 rounded-md border border-border flex items-center justify-center text-muted hover:text-[var(--fg-1)] transition-colors"
        >
          {theme === 'dark' ? <Sun size={15} /> : <Moon size={15} />}
        </button>
        <div className="w-8 h-8 rounded-full bg-[var(--blue)] text-white flex items-center justify-center text-[12px] font-semibold">LP</div>
      </div>
    </header>
  )
}
