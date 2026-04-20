import type { ReactNode } from 'react'
import { Sidebar } from './Sidebar'
import { Header } from './Header'

interface LayoutProps {
  children: ReactNode
  currentPath: string
  title: string
  subtitle?: string | undefined
}

export function Layout({ children, currentPath, title, subtitle }: LayoutProps) {
  return (
    <div className="flex h-screen">
      <Sidebar currentPath={currentPath} />
      <div className="flex-1 flex flex-col min-w-0 bg-[var(--bg-1)]">
        {/* Only spread subtitle when provided to satisfy exactOptionalPropertyTypes */}
        <Header title={title} {...(subtitle ? { subtitle } : {})} />
        <main className="flex-1 overflow-auto">{children}</main>
      </div>
    </div>
  )
}
