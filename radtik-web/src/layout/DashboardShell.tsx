import { Outlet } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { useUiStore } from '../store/uiStore'
import { navByRole } from '../navigation/navConfig'
import type { UserRole } from '../types'
import { Header } from './Header'
import { Sidebar } from './Sidebar'
import { MobileNav } from './MobileNav'

export function DashboardShell() {
  const role = useAuthStore((s) => s.user?.role) as UserRole | undefined
  const sidebarOpen = useUiStore((s) => s.sidebarOpen)
  const setSidebarOpen = useUiStore((s) => s.setSidebarOpen)
  const toggleSidebar = useUiStore((s) => s.toggleSidebar)

  if (!role) return null

  const items = navByRole[role]

  return (
    <div className="flex min-h-dvh bg-rt-page">
      <Sidebar items={items} open={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <div className="flex min-w-0 flex-1 flex-col pb-16 md:pb-0">
        <Header onMenuClick={() => toggleSidebar()} />
        <main className="flex-1 space-y-6 p-4 md:p-6">
          <Outlet />
        </main>
      </div>
      <MobileNav items={items} />
    </div>
  )
}
