import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import * as Sentry from '@sentry/react'
import './index.css'
import App from './App'
import { queryClient } from './lib/queryClient'
import { reportWebVitals } from './lib/web-vitals'
import { ErrorFallback } from './components/ui/ErrorFallback'

// ----------------------------------------------------------------------------
// Sentry bootstrap (Phase 7.3 observability)
// ----------------------------------------------------------------------------
// DSN wired via env var so the repo never ships with a baked-in project id.
// Init is a no-op when VITE_SENTRY_DSN is empty — typical for local dev.
// replaysSessionSampleRate is explicitly 0 for the MVP: session replays carry
// real bandwidth + privacy cost and the error-only replay path is enough for
// triage at this stage. Revisit in Phase 7.7 after the paper-trading window.
const sentryDsn = import.meta.env.VITE_SENTRY_DSN
if (sentryDsn) {
  Sentry.init({
    dsn: sentryDsn,
    integrations: [Sentry.browserTracingIntegration()],
    tracesSampleRate: 0.1,
    replaysSessionSampleRate: 0,
    replaysOnErrorSampleRate: 0.5,
    environment: import.meta.env.MODE
  })
}

// Theme initialization now handled by themeStore.ts (line 33) via index.html
// anti-flash script to prevent FOUC. The Header component imports themeStore,
// which triggers module load and applies the initial theme from localStorage.

const rootElement = document.getElementById('root')
if (!rootElement) {
  throw new Error('Root element not found')
}

createRoot(rootElement).render(
  <StrictMode>
    <Sentry.ErrorBoundary fallback={<ErrorFallback />}>
      <QueryClientProvider client={queryClient}>
        <App />
      </QueryClientProvider>
    </Sentry.ErrorBoundary>
  </StrictMode>
)

// Start the web-vitals reporter once the app is mounted. Safe to call even
// when Sentry is disabled — the Worker accepts the event independently.
reportWebVitals()
