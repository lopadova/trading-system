import { useState, useEffect } from 'react'
import { Layout } from './components/layout/Layout'
import { HomePage } from './pages/HomePage'
import { PositionsPage } from './pages/PositionsPage'
import { AlertsPage } from './pages/AlertsPage'
import { CampaignsPage } from './pages/CampaignsPage'
import { SettingsPage } from './pages/SettingsPage'
import { StrategyWizardPage } from './pages/StrategyWizardPage'
import { StrategyImportPage } from './pages/StrategyImportPage'
import { StrategyConvertPage } from './pages/StrategyConvertPage'
import { ToastContainer } from './components/ui/Toast'

type Route =
  | '/'
  | '/positions'
  | '/campaigns'
  | '/health'
  | '/ivts'
  | '/alerts'
  | '/logs'
  | '/settings'
  | '/strategies/new'
  | '/strategies/import'
  | '/strategies/convert'
  | `/strategies/${string}/edit`

function App() {
  const [currentRoute, setCurrentRoute] = useState<Route>('/')

  useEffect(() => {
    // Simple client-side routing
    const handleNavigation = (e: MouseEvent) => {
      const target = e.target as HTMLElement
      const anchor = target.closest('a')
      if (anchor && anchor.href.startsWith(window.location.origin)) {
        e.preventDefault()
        const path = anchor.pathname as Route
        setCurrentRoute(path)
        window.history.pushState({}, '', path)
      }
    }

    const handlePopState = () => {
      setCurrentRoute(window.location.pathname as Route)
    }

    document.addEventListener('click', handleNavigation)
    window.addEventListener('popstate', handlePopState)

    return () => {
      document.removeEventListener('click', handleNavigation)
      window.removeEventListener('popstate', handlePopState)
    }
  }, [])

  const renderPage = () => {
    // Handle dynamic routes
    if (currentRoute.startsWith('/strategies/') && currentRoute.includes('/edit')) {
      const strategyId = currentRoute.split('/')[2] || ''
      if (!strategyId) {
        return (
          <div className="text-center py-12">
            <h1 className="text-2xl font-bold mb-2">Invalid Strategy ID</h1>
            <p className="text-muted">Strategy ID is required for edit mode</p>
          </div>
        )
      }
      return <StrategyWizardPage mode="edit" strategyId={strategyId} />
    }

    // Handle static routes
    switch (currentRoute) {
      case '/positions':
        return <PositionsPage />
      case '/campaigns':
        return <CampaignsPage />
      case '/alerts':
        return <AlertsPage />
      case '/settings':
        return <SettingsPage />
      case '/strategies/new':
        return <StrategyWizardPage mode="new" />
      case '/strategies/import':
        return <StrategyImportPage />
      case '/strategies/convert':
        return <StrategyConvertPage />
      case '/':
        return <HomePage />
      default:
        return (
          <div className="text-center py-12">
            <h1 className="text-2xl font-bold mb-2">Page Not Implemented</h1>
            <p className="text-muted">This page is coming soon</p>
          </div>
        )
    }
  }

  return (
    <>
      <Layout>{renderPage()}</Layout>
      <ToastContainer />
    </>
  )
}

export default App
