export type ThemeMode = 'dark' | 'light' | 'system'

export function applyTheme(theme: ThemeMode): void {
  const root = document.documentElement
  const resolved =
    theme === 'system'
      ? window.matchMedia('(prefers-color-scheme: dark)').matches
        ? 'dark'
        : 'light'
      : theme
  root.setAttribute('data-theme', resolved)
}

export function initThemeListener(onThemeChange: (theme: ThemeMode) => void): () => void {
  const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)')
  const listener = () => onThemeChange('system')
  mediaQuery.addEventListener('change', listener)
  return () => mediaQuery.removeEventListener('change', listener)
}
