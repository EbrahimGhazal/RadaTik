import { useCallback, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useUiStore } from '../store/uiStore'
import { navByRole } from '../navigation/navConfig'
import { usePageMeta } from '../navigation/usePageMeta'
import type { UserRole } from '../types'
import { PageHeader } from '../components/ui/PageHeader'
import { Header } from './Header'
import { Sidebar } from './Sidebar'
import { MobileNav } from './MobileNav'
import { QuickNav, useQuickNavShortcut } from './QuickNav'

export function DashboardShell() {
  const role = useAuthStore((s) => s.user?.role) as UserRole | undefined
  const sidebarOpen = useUiStore((s) => s.sidebarOpen)
  const setSidebarOpen = useUiStore((s) => s.setSidebarOpen)
  const toggleSidebar = useUiStore((s) => s.toggleSidebar)
  const [quickNavOpen, setQuickNavOpen] = useState(false)

  const openQuickNav = useCallback(() => setQuickNavOpen(true), [])
  useQuickNavShortcut(openQuickNav)

  const { title, breadcrumbs } = usePageMeta(role)

  if (!role) return null

  const items = navByRole[role]

  return (
    <div className="rt-app-bg flex min-h-dvh">
      <Sidebar
        items={items}
        open={sidebarOpen}
        onClose={() => setSidebarOpen(false)}
        onQuickNavOpen={openQuickNav}
      />
      <div className="flex min-w-0 flex-1 flex-col pb-[calc(4rem+env(safe-area-inset-bottom))] md:pb-0">
        <Header onMenuClick={() => toggleSidebar()} onQuickNavOpen={openQuickNav} />
        <main className="mx-auto w-full max-w-[1600px] flex-1 space-y-6 p-4 sm:p-5 md:p-6 lg:p-8 xl:px-10">
          <PageHeader title={title} breadcrumbs={breadcrumbs} />
          <Outlet />
        </main>
      </div>
      <MobileNav items={items} />
      <QuickNav items={items} open={quickNavOpen} onClose={() => setQuickNavOpen(false)} />
    </div>
  )
}
