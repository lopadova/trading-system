/**
 * ErrorFallback — rendered by Sentry.ErrorBoundary when an unhandled render
 * error bubbles up. The fallback stays inside the existing design kit (Card +
 * Button primitives) so the page doesn't feel like it left the app.
 *
 * Intentionally minimal: a title, a terse message, a "Reload" button. Debug
 * details already flow to Sentry via the wrapping ErrorBoundary; showing a
 * full stack here would leak internals and add no user value.
 */

import { Card } from './Card'
import { Button } from './Button'

export interface ErrorFallbackProps {
  error?: unknown
  resetError?: () => void
}

export function ErrorFallback({ resetError }: ErrorFallbackProps) {
  // Soft reset first; if that doesn't fix things (e.g. corrupted window state),
  // a full page reload is the safe escape hatch.
  const handleReload = () => {
    if (resetError) resetError()
    // Reload unconditionally — resetError only re-mounts subtree, which may
    // not clear singleton React Query caches that were also corrupted.
    window.location.reload()
  }

  return (
    <div className="flex items-center justify-center min-h-[60vh] p-6">
      <Card padding={24} className="max-w-md w-full text-center">
        <h1 className="text-xl font-semibold mb-2">Something went wrong</h1>
        <p className="text-muted text-sm mb-5">
          The dashboard hit an unexpected error. Our team has been notified.
        </p>
        <Button variant="primary" onClick={handleReload}>
          Reload
        </Button>
      </Card>
    </div>
  )
}
