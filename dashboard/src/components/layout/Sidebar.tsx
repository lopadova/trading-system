import {
  Home,
  Activity,
  TrendingUp,
  Settings,
  FileText,
  BarChart3,
  AlertCircle,
  FolderKanban,
} from 'lucide-react'
import { useUiStore } from '../../stores/uiStore'
import { cn } from '../../utils/cn'

interface NavItem {
  icon: typeof Home
  label: string
  href: string
  badge?: number
}

const navItems: NavItem[] = [
  { icon: Home, label: 'Overview', href: '/' },
  { icon: Activity, label: 'System Health', href: '/health' },
  { icon: TrendingUp, label: 'Positions', href: '/positions' },
  { icon: FolderKanban, label: 'Campaigns', href: '/campaigns' },
  { icon: BarChart3, label: 'IVTS Monitor', href: '/ivts' },
  { icon: AlertCircle, label: 'Alerts', href: '/alerts', badge: 0 },
  { icon: FileText, label: 'Logs', href: '/logs' },
  { icon: Settings, label: 'Settings', href: '/settings' },
]

export function Sidebar() {
  const { sidebarOpen } = useUiStore()

  return (
    <aside
      className={cn(
        'border-r border-border bg-background transition-all duration-300',
        'flex flex-col',
        sidebarOpen ? 'w-64' : 'w-0 overflow-hidden'
      )}
    >
      <nav className="flex-1 p-4 space-y-2">
        {navItems.map((item) => (
          <a
            key={item.href}
            href={item.href}
            className={cn(
              'flex items-center gap-3 px-3 py-2 rounded-lg',
              'text-foreground hover:bg-muted/10 transition-colors',
              'group relative'
            )}
          >
            <item.icon className="h-5 w-5 text-muted group-hover:text-foreground transition-colors" />
            <span className="text-sm font-medium">{item.label}</span>
            {item.badge !== undefined && item.badge > 0 && (
              <span className="ml-auto bg-danger text-white text-xs font-semibold px-2 py-0.5 rounded-full">
                {item.badge}
              </span>
            )}
          </a>
        ))}
      </nav>

      <div className="p-4 border-t border-border text-xs text-muted">
        <p>Trading System v0.1.0</p>
        <p className="mt-1">Mode: Paper</p>
      </div>
    </aside>
  )
}
