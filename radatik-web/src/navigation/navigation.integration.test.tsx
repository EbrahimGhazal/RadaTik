import { describe, expect, it, beforeEach } from 'vitest'
import { fireEvent, render, waitFor } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import '../i18n'
import { useAuthStore } from '../store/authStore'
import { navByRole } from './navConfig'
import { ROLE_ROUTE, type AuthUser, type UserRole } from '../types'
import { ProtectedRoute } from '../routes/ProtectedRoute'
import { RoleGate } from '../routes/RoleGate'
import { DashboardShell } from '../layout/DashboardShell'
import { AdminDashboard } from '../pages/dashboards/AdminDashboard'
import { AdminUsersPage } from '../pages/admin/AdminUsersPage'
import { AdminNetworksPage } from '../pages/admin/AdminNetworksPage'
import { AdminPaymentsPage } from '../pages/admin/AdminPaymentsPage'
import { AdminReportsPage } from '../pages/admin/AdminReportsPage'
import { AdminSettingsPage } from '../pages/admin/AdminSettingsPage'

function makeUser(role: UserRole): AuthUser {
  return {
    id: 'test-user',
    email: 'test@example.com',
    fullName: 'Test User',
    role,
  }
}

function AdminTestApp({ initialPath }: { initialPath: string }) {
  return (
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
          <Route element={<ProtectedRoute />}>
            <Route
              path="/admin"
              element={
                <RoleGate role="system_admin">
                  <DashboardShell />
                </RoleGate>
              }
            >
              <Route index element={<AdminDashboard />} />
              <Route path="users" element={<AdminUsersPage />} />
              <Route path="networks" element={<AdminNetworksPage />} />
              <Route path="payments" element={<AdminPaymentsPage />} />
              <Route path="reports" element={<AdminReportsPage />} />
              <Route path="settings" element={<AdminSettingsPage />} />
            </Route>
          </Route>
        </Routes>
    </MemoryRouter>
  )
}

describe('sidebar navigation integration', () => {
  beforeEach(() => {
    useAuthStore.setState({ user: makeUser('system_admin'), isHydrating: false })
  })

  it('renders every admin nav target without crashing', async () => {
    for (const item of navByRole.system_admin) {
      const { unmount } = render(<AdminTestApp initialPath={item.to} />)
      await waitFor(() => {
        expect(document.querySelector('main')).toBeTruthy()
      })
      unmount()
    }
  })

  it('navigates between admin tabs via sidebar links', async () => {
    render(<AdminTestApp initialPath={ROLE_ROUTE.system_admin} />)

    await waitFor(() => {
      expect(document.querySelector('main')).toBeTruthy()
    })

    for (const item of navByRole.system_admin) {
      if (item.to === ROLE_ROUTE.system_admin) continue
      const link = document.querySelector(`a[href="${item.to}"]`)
      expect(link).toBeTruthy()
      fireEvent.click(link!)
      await waitFor(() => {
        expect(document.querySelector(`a[href="${item.to}"][aria-current="page"]`)).toBeTruthy()
      })
    }
  })
})
