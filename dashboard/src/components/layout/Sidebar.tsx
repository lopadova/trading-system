import {
  Home, Activity, TrendingUp, FolderKanban, BarChart3,
  AlertCircle, FileText, Settings as SettingsIcon, Sparkles
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { cn } from '../../utils/cn'

interface NavItem {
  path: string
  icon: LucideIcon
  label: string
  badge?: number
}

const items: NavItem[] = [
  { path: '/', icon: Home, label: 'Overview' },
  { path: '/health', icon: Activity, label: 'System Health' },
  { path: '/positions', icon: TrendingUp, label: 'Positions' },
  { path: '/campaigns', icon: FolderKanban, label: 'Campaigns' },
  { path: '/ivts', icon: BarChart3, label: 'IVTS Monitor' },
  { path: '/alerts', icon: AlertCircle, label: 'Alerts' },
  { path: '/logs', icon: FileText, label: 'Logs' },
  { path: '/strategies/new', icon: Sparkles, label: 'Strategy Wizard' },
  { path: '/settings', icon: SettingsIcon, label: 'Settings' },
]

interface SidebarProps {
  currentPath: string
}

function Logo({ size = 22 }: { size?: number }) {
  return (
    <svg width={size} height={size * 28 / 30} viewBox="0 0 30 28" fill="none" aria-label="Trading System logo">
      <path d="M19 2 L5 15 L13 15 L11 26 L25 13 L17 13 L19 2 Z" fill="var(--purple)" stroke="var(--purple)" strokeWidth="1.2" strokeLinejoin="round" />
    </svg>
  )
}

export function Sidebar({ currentPath }: SidebarProps) {
  return (
    <aside className="w-60 bg-[var(--bg-1)] border-r border-border flex flex-col h-screen shrink-0">
      <div className="flex items-center gap-2.5 h-16 px-4 border-b border-border">
        <Logo />
        <div>
          <div className="font-display font-bold text-[14px] tracking-tight text-[var(--fg-1)]">Trading System</div>
          <div className="text-[10.5px] text-muted font-mono">dashboard · v0.1.0</div>
        </div>
      </div>
      <nav className="flex-1 p-2.5 overflow-auto flex flex-col gap-0.5">
        {items.map(it => {
          const active = currentPath === it.path || (it.path === '/strategies/new' && currentPath.startsWith('/strategies'))
          return (
            <a
              key={it.path}
              href={it.path}
              className={cn(
                'flex items-center gap-2.5 px-2.5 py-1.5 rounded-md text-[13px] transition-colors',
                active ? 'bg-[var(--tint-blue)] text-[var(--blue)] font-medium' : 'text-[var(--fg-1)] hover:bg-[color:var(--fg-2)]/10'
              )}
            >
              <it.icon size={16} className={active ? 'text-[var(--blue)]' : 'text-muted'} />
              <span className="flex-1">{it.label}</span>
              {it.badge != null && (
                <span className="bg-[var(--red)] text-white text-[10px] font-semibold px-1.5 py-[1px] rounded-full">{it.badge}</span>
              )}
            </a>
          )
        })}
      </nav>
      <div className="border-t border-border px-4 py-3 text-[11px] text-muted flex items-center justify-between">
        <div>
          <div>Paper Trading</div>
          <div className="font-mono">IBKR · Connected</div>
        </div>
        <span className="w-2 h-2 rounded-full bg-[var(--green)] pulse-dot" style={{ boxShadow: '0 0 6px var(--green)' }} />
      </div>
    </aside>
  )
}
