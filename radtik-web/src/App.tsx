import { Suspense, lazy, useEffect, useLayoutEffect } from 'react'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { useDocumentTheme } from './hooks/useDocumentTheme'
import { useAuthStore } from './store/authStore'
import { useUiStore } from './store/uiStore'
import { ROLE_ROUTE } from './types'
import { ProtectedRoute } from './routes/ProtectedRoute'
import { RoleGate } from './routes/RoleGate'
import { DashboardShell } from './layout/DashboardShell'

const Login = lazy(() => import('./pages/auth/Login').then((m) => ({ default: m.Login })))
const Signup = lazy(() => import('./pages/auth/Signup').then((m) => ({ default: m.Signup })))
const AdminDashboard = lazy(() => import('./pages/dashboards/AdminDashboard').then((m) => ({ default: m.AdminDashboard })))
const EmployeeDashboard = lazy(() => import('./pages/dashboards/EmployeeDashboard').then((m) => ({ default: m.EmployeeDashboard })))
const ManagerDashboard = lazy(() => import('./pages/dashboards/ManagerDashboard').then((m) => ({ default: m.ManagerDashboard })))
const ClientDashboard = lazy(() => import('./pages/dashboards/ClientDashboard').then((m) => ({ default: m.ClientDashboard })))
const CollectionDashboard = lazy(() =>
  import('./pages/dashboards/CollectionDashboard').then((m) => ({ default: m.CollectionDashboard })),
)
const AdminUsersPage = lazy(() => import('./pages/admin/AdminUsersPage').then((m) => ({ default: m.AdminUsersPage })))
const AdminNetworksPage = lazy(() => import('./pages/admin/AdminNetworksPage').then((m) => ({ default: m.AdminNetworksPage })))
const AdminPaymentsPage = lazy(() => import('./pages/admin/AdminPaymentsPage').then((m) => ({ default: m.AdminPaymentsPage })))
const AdminReportsPage = lazy(() => import('./pages/admin/AdminReportsPage').then((m) => ({ default: m.AdminReportsPage })))
const AdminSettingsPage = lazy(() => import('./pages/admin/AdminSettingsPage').then((m) => ({ default: m.AdminSettingsPage })))
const EmployeeSupportPage = lazy(() =>
  import('./pages/employee/EmployeeSupportPage').then((m) => ({ default: m.EmployeeSupportPage })),
)
const EmployeeTasksPage = lazy(() =>
  import('./pages/employee/EmployeeTasksPage').then((m) => ({ default: m.EmployeeTasksPage })),
)
const EmployeePaymentsPage = lazy(() =>
  import('./pages/employee/EmployeePaymentsPage').then((m) => ({ default: m.EmployeePaymentsPage })),
)
const ManagerBillingPage = lazy(() => import('./pages/manager/ManagerBillingPage').then((m) => ({ default: m.ManagerBillingPage })))
const ManagerTeamPage = lazy(() => import('./pages/manager/ManagerTeamPage').then((m) => ({ default: m.ManagerTeamPage })))
const ManagerReportsPage = lazy(() => import('./pages/manager/ManagerReportsPage').then((m) => ({ default: m.ManagerReportsPage })))
const ManagerSettingsPage = lazy(() =>
  import('./pages/manager/ManagerSettingsPage').then((m) => ({ default: m.ManagerSettingsPage })),
)
const ManagerMikrotikTrafficPage = lazy(() =>
  import('./pages/manager/ManagerMikrotikTrafficPage').then((m) => ({ default: m.ManagerMikrotikTrafficPage })),
)
const ClientNetworkPage = lazy(() => import('./pages/client/ClientNetworkPage').then((m) => ({ default: m.ClientNetworkPage })))
const ClientTrafficPage = lazy(() => import('./pages/client/ClientTrafficPage').then((m) => ({ default: m.ClientTrafficPage })))
const ClientBillingPage = lazy(() => import('./pages/client/ClientBillingPage').then((m) => ({ default: m.ClientBillingPage })))
const ClientSupportPage = lazy(() => import('./pages/client/ClientSupportPage').then((m) => ({ default: m.ClientSupportPage })))
const CollectionTransactionsPage = lazy(() =>
  import('./pages/collection/CollectionTransactionsPage').then((m) => ({ default: m.CollectionTransactionsPage })),
)
const CollectionPaymentsPage = lazy(() =>
  import('./pages/collection/CollectionPaymentsPage').then((m) => ({ default: m.CollectionPaymentsPage })),
)
const CollectionSettingsPage = lazy(() =>
  import('./pages/collection/CollectionSettingsPage').then((m) => ({ default: m.CollectionSettingsPage })),
)

function ThemeBootstrap() {
  useLayoutEffect(() => {
    try {
      const raw = localStorage.getItem('radtik-ui')
      if (!raw) return
      const theme = (JSON.parse(raw) as { state?: { theme?: string } }).state?.theme
      document.documentElement.classList.toggle('dark', theme === 'dark')
    } catch {
      /* ignore */
    }
  }, [])
  return null
}

function ThemeAndLang() {
  const theme = useUiStore((s) => s.theme)
  const lang = useUiStore((s) => s.lang)
  useDocumentTheme(theme, lang)
  return null
}

function RootRedirect() {
  const user = useAuthStore((s) => s.user)
  if (user) return <Navigate to={ROLE_ROUTE[user.role]} replace />
  return <Navigate to="/login" replace />
}

const routerBasename = (import.meta.env.BASE_URL || '/').replace(/\/$/, '') || '/'

export default function App() {
  const hydrateFromServer = useAuthStore((s) => s.hydrateFromServer)
  const isHydrating = useAuthStore((s) => s.isHydrating)

  useEffect(() => {
    void hydrateFromServer()
  }, [hydrateFromServer])

  return (
    <BrowserRouter basename={routerBasename}>
      <ThemeBootstrap />
      <ThemeAndLang />
      {isHydrating ? <div className="min-h-dvh bg-rt-page" /> : null}
      <Suspense fallback={<div className="min-h-dvh bg-rt-page" />}>
        <Routes>
          <Route path="/" element={<RootRedirect />} />
          <Route path="/login" element={<Login />} />
          <Route path="/signup" element={<Signup />} />

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

            <Route
              path="/employee"
              element={
                <RoleGate role="employee">
                  <DashboardShell />
                </RoleGate>
              }
            >
              <Route index element={<EmployeeDashboard />} />
              <Route path="support" element={<EmployeeSupportPage />} />
              <Route path="tasks" element={<EmployeeTasksPage />} />
              <Route path="payments" element={<EmployeePaymentsPage />} />
            </Route>

            <Route
              path="/manager"
              element={
                <RoleGate role="company_manager">
                  <DashboardShell />
                </RoleGate>
              }
            >
              <Route index element={<ManagerDashboard />} />
              <Route path="mikrotik-traffic" element={<ManagerMikrotikTrafficPage />} />
              <Route path="billing" element={<ManagerBillingPage />} />
              <Route path="team" element={<ManagerTeamPage />} />
              <Route path="reports" element={<ManagerReportsPage />} />
              <Route path="settings" element={<ManagerSettingsPage />} />
            </Route>

            <Route
              path="/client"
              element={
                <RoleGate role="client">
                  <DashboardShell />
                </RoleGate>
              }
            >
              <Route index element={<ClientDashboard />} />
              <Route path="network" element={<ClientNetworkPage />} />
              <Route path="traffic" element={<ClientTrafficPage />} />
              <Route path="billing" element={<ClientBillingPage />} />
              <Route path="support" element={<ClientSupportPage />} />
            </Route>

            <Route
              path="/collection"
              element={
                <RoleGate role="collection_point">
                  <DashboardShell />
                </RoleGate>
              }
            >
              <Route index element={<CollectionDashboard />} />
              <Route path="transactions" element={<CollectionTransactionsPage />} />
              <Route path="payments" element={<CollectionPaymentsPage />} />
              <Route path="settings" element={<CollectionSettingsPage />} />
            </Route>
          </Route>

          <Route path="*" element={<RootRedirect />} />
        </Routes>
      </Suspense>
    </BrowserRouter>
  )
}
