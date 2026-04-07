# Skill: React 18 + TanStack + Tailwind v4 — Dashboard Pattern
> Aggiornabile dagli agenti. Controlla knowledge/errors-registry.md per fix noti.

---

## Setup Progetto

> ⚠️ AGGIORNATO da TASK-06 — 2026-04-05
> Motivo: Tailwind CSS v4 requires @tailwindcss/postcss separate package
> Fix: Install @tailwindcss/postcss and use in postcss.config.js

```bash
bun create vite dashboard --template react-ts
cd dashboard
bun add @tanstack/react-router@latest @tanstack/react-query@latest
bun add zustand@latest ky@latest date-fns@latest clsx tailwind-merge
bun add motion lucide-react
bun add -d tailwindcss@latest @tailwindcss/postcss@latest postcss@latest autoprefixer@latest
bun add -d prettier@latest prettier-plugin-tailwindcss@latest
bun add -d wrangler @cloudflare/workers-types
```

**PostCSS config** (postcss.config.js):
```js
export default {
  plugins: {
    '@tailwindcss/postcss': {},  // NOT 'tailwindcss' directly
    autoprefixer: {},
  },
}
```

## tsconfig.json (strict mode obbligatorio)

```json
{
  "compilerOptions": {
    "target": "ES2022",
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "exactOptionalPropertyTypes": true,
    "moduleResolution": "Bundler",
    "jsx": "react-jsx"
  }
}
```

## TanStack Query — Pattern

```typescript
// QueryClient globale con defaults sensati
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 10_000,          // 10s prima di refetch
      gcTime:    5 * 60_000,      // 5min in cache
      retry: 2,
      refetchOnWindowFocus: true,
    },
  },
})

// Hook tipizzato per ogni endpoint
export function useHeartbeat() {
  return useQuery({
    queryKey: ['system', 'heartbeat'],
    queryFn: async (): Promise<HeartbeatData> => {
      const res = await ky.get('/api/v1/system/heartbeats', {
        headers: { 'X-Api-Key': import.meta.env.VITE_API_KEY },
        searchParams: { limit: '1' },
      }).json<HeartbeatData>()
      return res
    },
    refetchInterval: 15_000,    // refresh ogni 15s
    select: (data) => data,     // trasformazione opzionale
  })
}
```

## Zustand Store — Solo stato UI

```typescript
// Store per stato UI (tema, sidebar, filtri) — NON per dati server
interface UiStore {
  theme:      'dark' | 'light' | 'system'
  sidebarOpen: boolean
  setTheme:   (t: UiStore['theme']) => void
  toggleSidebar: () => void
}

export const useUiStore = create<UiStore>()(
  persist(
    (set) => ({
      theme: 'system',
      sidebarOpen: true,
      setTheme: (theme) => {
        set({ theme })
        applyTheme(theme)  // applica data-theme su <html>
      },
      toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
    }),
    { name: 'trading-ui', partialize: (s) => ({ theme: s.theme }) }
  )
)
```

## Tema Dark/Light/System

```typescript
// utils/theme.ts
export function applyTheme(theme: 'dark' | 'light' | 'system'): void {
  const root = document.documentElement
  const resolved = theme === 'system'
    ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
    : theme
  root.setAttribute('data-theme', resolved)
}

// Anti-flash script — da mettere in index.html <head> PRIMA di qualsiasi script
// <script>
//   (function() {
//     const t = localStorage.getItem('trading-ui');
//     const theme = t ? JSON.parse(t).state?.theme : 'system';
//     const resolved = theme === 'system'
//       ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'
//       : theme || 'dark';
//     document.documentElement.setAttribute('data-theme', resolved);
//   })();
// </script>
```

## ECharts — Pattern React

```typescript
// Wrapper tipizzato per ECharts
import ReactECharts from 'echarts-for-react'
import type { EChartsOption } from 'echarts'

interface IvtsChartProps {
  data: Array<{ date: string; value: number }>
}

export function IvtsChart({ data }: IvtsChartProps) {
  const option: EChartsOption = {
    backgroundColor: 'transparent',
    xAxis: { type: 'time' },
    yAxis: { type: 'value', min: 0.8, max: 1.5 },
    series: [
      { type: 'line', data: data.map(d => [d.date, d.value]),
        smooth: true, lineStyle: { color: 'var(--color-accent)' } }
    ],
    // Linee soglia IVTS
    markLine: { data: [
      { yAxis: 1.15, name: 'Suspend', lineStyle: { color: '#ef4444' } },
      { yAxis: 1.10, name: 'Resume',  lineStyle: { color: '#22c55e' } },
    ]}
  }
  return <ReactECharts option={option} style={{ height: 300 }} notMerge />
}
```

## Componente con Loading/Error State

```typescript
export function MachineHealthWidget() {
  const { data, isLoading, isError, error } = useHeartbeat()

  if (isLoading) return <WidgetSkeleton />
  if (isError)   return <WidgetError message={String(error)} />
  if (!data)     return <WidgetEmpty message="No heartbeat data" />

  const age = differenceInMinutes(new Date(), parseISO(data.timestamp))
  const freshness: 'ok' | 'warn' | 'stale' =
    age < 3 ? 'ok' : age < 10 ? 'warn' : 'stale'

  return (
    <Card>
      <CardHeader>
        <CardTitle>Machine Health</CardTitle>
        <StalenessBadge age={age} freshness={freshness} />
      </CardHeader>
      <CardContent>
        <MetricRow label="CPU"  value={`${data.cpu_pct.toFixed(1)}%`} />
        <MetricRow label="RAM"  value={`${data.ram_pct.toFixed(1)}%`} />
        <MetricRow label="Disk" value={`${data.disk_free_gb.toFixed(1)} GB`} />
      </CardContent>
    </Card>
  )
}
```

## Anti-pattern React

```typescript
// ❌ useEffect per data fetching
useEffect(() => {
  fetch('/api/data').then(r => r.json()).then(setData)
}, [])

// ✅ CORRETTO — useQuery
const { data } = useQuery({ queryKey: ['data'], queryFn: fetchData })

// ❌ any nei tipi
const data: any = await response.json()

// ✅ CORRETTO — tipizzazione esplicita
const data: HeartbeatResponse = await response.json<HeartbeatResponse>()

// ❌ Colori hardcoded (rompono il tema)
<div style={{ color: '#ffffff' }}>

// ✅ CORRETTO — CSS variables del tema
<div className="text-foreground">
```
