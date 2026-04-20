import { Menu, Moon, Sun, Monitor } from 'lucide-react'
import { useUiStore } from '../../stores/uiStore'
import { cn } from '../../utils/cn'

export function Header() {
  const { theme, setTheme, toggleSidebar } = useUiStore()

  const cycleTheme = () => {
    const nextTheme: Record<typeof theme, typeof theme> = {
      light: 'dark',
      dark: 'system',
      system: 'light',
    }
    setTheme(nextTheme[theme])
  }

  const ThemeIcon = theme === 'light' ? Sun : theme === 'dark' ? Moon : Monitor

  return (
    <header className="border-b border-[#1a2332] sticky top-0 z-50" style={{
      background: 'linear-gradient(135deg, rgba(15, 20, 27, 0.95) 0%, rgba(21, 27, 36, 0.9) 100%)',
      backdropFilter: 'blur(20px)',
      boxShadow: '0 4px 24px rgba(0, 0, 0, 0.2), inset 0 1px 0 rgba(255, 255, 255, 0.05)'
    }}>
      <div className="flex h-16 items-center justify-between px-6">
        <div className="flex items-center gap-4">
          <button
            onClick={toggleSidebar}
            className="rounded-lg p-2 hover:bg-white/5 transition-all duration-200 border border-transparent hover:border-[#253145]"
            aria-label="Toggle sidebar"
          >
            <Menu className="h-5 w-5" />
          </button>
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-blue-500 to-cyan-500 flex items-center justify-center font-bold text-white text-sm" style={{ boxShadow: '0 0 20px rgba(59, 130, 246, 0.3)' }}>
              TS
            </div>
            <h1 className="text-xl font-bold tracking-tight">
              Trading System <span className="text-transparent bg-clip-text bg-gradient-to-r from-blue-400 to-cyan-400">Dashboard</span>
            </h1>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={cycleTheme}
            className={cn(
              'rounded-lg p-2 px-3 hover:bg-white/5 transition-all duration-200',
              'flex items-center gap-2 border border-transparent hover:border-[#253145]'
            )}
            aria-label={`Current theme: ${theme}. Click to cycle.`}
          >
            <ThemeIcon className="h-4 w-4" />
            <span className="text-xs font-mono text-[#8b95a8] capitalize hidden sm:inline font-semibold tracking-wide">{theme}</span>
          </button>
        </div>
      </div>
    </header>
  )
}
