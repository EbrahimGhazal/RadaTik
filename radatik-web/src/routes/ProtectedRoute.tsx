import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'

/** Require authentication; guest users redirect to login. */
export function ProtectedRoute() {
  const user = useAuthStore((s) => s.user)
  const isHydrating = useAuthStore((s) => s.isHydrating)
  const location = useLocation()

  if (isHydrating) {
    return <div className="min-h-dvh bg-rt-page" aria-busy="true" />
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location.pathname }} />
  }

  return <Outlet />
}
