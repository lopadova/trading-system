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
  const currentPath = window.location.pathname

  return (
    <aside
      className={cn(
        'border-r border-[#1a2332] transition-all duration-300',
        'flex flex-col relative',
        sidebarOpen ? 'w-64' : 'w-0 overflow-hidden'
      )}
      style={{
        background: 'linear-gradient(135deg, rgba(15, 20, 27, 0.9) 0%, rgba(10, 14, 20, 0.95) 100%)',
        backdropFilter: 'blur(20px)',
      }}
    >
      <nav className="flex-1 p-4 space-y-1.5">
        {navItems.map((item) => {
          const isActive = currentPath === item.href
          return (
            <a
              key={item.href}
              href={item.href}
              className={cn(
                'flex items-center gap-3 px-3 py-2.5 rounded-lg',
                'transition-all duration-200',
                'group relative overflow-hidden',
                isActive
                  ? 'bg-white/8 border border-[#253145] text-white'
                  : 'text-[#8b95a8] hover:bg-white/5 border border-transparent hover:border-[#253145]'
              )}
            >
              {isActive && (
                <div
                  className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-blue-400 to-cyan-400"
                  style={{ boxShadow: '0 0 12px rgba(59, 130, 246, 0.5)' }}
                />
              )}
              <item.icon className={cn(
                'h-4 w-4 transition-colors',
                isActive ? 'text-blue-400' : 'text-[#5a6575] group-hover:text-[#8b95a8]'
              )} />
              <span className={cn(
                'text-sm font-medium tracking-tight',
                isActive && 'font-semibold'
              )}>{item.label}</span>
              {item.badge !== undefined && item.badge > 0 && (
                <span className="ml-auto bg-red-500/20 text-red-400 text-[10px] font-mono font-bold px-2 py-0.5 rounded border border-red-500/30" style={{ boxShadow: '0 0 8px rgba(239, 68, 68, 0.2)' }}>
                  {item.badge}
                </span>
              )}
            </a>
          )
        })}
      </nav>

      <div className="p-4 border-t border-[#1a2332] text-[10px] font-mono text-[#5a6575] space-y-1">
        <p className="font-semibold tracking-wider uppercase">Trading System v0.1.0</p>
        <div className="flex items-center gap-2">
          <span className="text-[#8b95a8]">Mode:</span>
          <span className="px-2 py-0.5 rounded bg-green-500/10 text-green-400 border border-green-500/20 font-bold">PAPER</span>
        </div>
      </div>
    </aside>
  )
}
