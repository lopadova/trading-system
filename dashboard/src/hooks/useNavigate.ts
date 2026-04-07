/**
 * Simple navigation hook
 *
 * Provides client-side navigation compatible with current routing setup.
 * Will be replaced/enhanced when TanStack Router is fully integrated.
 */

export function useNavigate() {
  return (path: string) => {
    window.history.pushState({}, '', path)
    window.dispatchEvent(new PopStateEvent('popstate'))
  }
}
