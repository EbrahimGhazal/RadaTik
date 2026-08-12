import type { ReactNode } from 'react'
import { Navigate } from 'react-router-dom'
import { useAuthStore } from '../store/authStore'
import { ROLE_ROUTE, type UserRole } from '../types'

/** Ensures the signed-in user matches the area role before rendering shell + nested routes. */
export function RoleGate({ role, children }: { role: UserRole; children: ReactNode }) {
  const user = useAuthStore((s) => s.user)

  if (!user) {
    return <Navigate to="/login" replace />
  }

  if (user.role !== role) {
    return <Navigate to={ROLE_ROUTE[user.role]} replace />
  }

  return <>{children}</>
}
