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
    <header className="border-b border-border bg-background sticky top-0 z-50">
      <div className="flex h-16 items-center justify-between px-4">
        <div className="flex items-center gap-4">
          <button
            onClick={toggleSidebar}
            className="rounded-lg p-2 hover:bg-muted/10 transition-colors"
            aria-label="Toggle sidebar"
          >
            <Menu className="h-5 w-5" />
          </button>
          <h1 className="text-xl font-semibold">Trading System Dashboard</h1>
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={cycleTheme}
            className={cn(
              'rounded-lg p-2 hover:bg-muted/10 transition-colors',
              'flex items-center gap-2'
            )}
            aria-label={`Current theme: ${theme}. Click to cycle.`}
          >
            <ThemeIcon className="h-5 w-5" />
            <span className="text-sm text-muted capitalize hidden sm:inline">{theme}</span>
          </button>
        </div>
      </div>
    </header>
  )
}
