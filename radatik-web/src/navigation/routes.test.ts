import { describe, expect, it } from 'vitest'
import { navByRole } from './navConfig'
import { ROLE_ROUTE, type UserRole } from '../types'

/**
 * Route segments registered under each role in App.tsx.
 * Update this list when adding or removing nested routes.
 */
const ROUTE_SEGMENTS: Record<UserRole, string[]> = {
  system_admin: ['', 'users', 'networks', 'payments', 'reports', 'settings'],
  employee: ['', 'support', 'tasks', 'payments'],
  company_manager: ['', 'mikrotik-traffic', 'billing', 'team', 'reports', 'settings'],
  client: ['', 'network', 'traffic', 'billing', 'support'],
  collection_point: ['', 'transactions', 'payments', 'settings'],
}

function toPath(prefix: string, segment: string): string {
  return segment === '' ? prefix : `${prefix}/${segment}`
}

const ALL_REGISTERED_PATHS = new Set(
  (Object.keys(ROUTE_SEGMENTS) as UserRole[]).flatMap((role) => {
    const prefix = ROLE_ROUTE[role]
    return ROUTE_SEGMENTS[role].map((segment) => toPath(prefix, segment))
  }),
)

describe('SPA route manifest', () => {
  it('registers a home route for every role', () => {
    for (const role of Object.keys(ROLE_ROUTE) as UserRole[]) {
      expect(ALL_REGISTERED_PATHS.has(ROLE_ROUTE[role])).toBe(true)
    }
  })

  it('keeps navByRole links aligned with App.tsx routes', () => {
    for (const role of Object.keys(navByRole) as UserRole[]) {
      const navPaths = navByRole[role].map((item) => item.to).sort()
      const routePaths = ROUTE_SEGMENTS[role].map((segment) => toPath(ROLE_ROUTE[role], segment)).sort()
      expect(navPaths).toEqual(routePaths)
    }
  })

  it('uses unique nav targets per role', () => {
    for (const role of Object.keys(navByRole) as UserRole[]) {
      const paths = navByRole[role].map((item) => item.to)
      expect(new Set(paths).size).toBe(paths.length)
    }
  })

  it('does not cross-link routes between roles', () => {
    for (const role of Object.keys(navByRole) as UserRole[]) {
      const prefix = ROLE_ROUTE[role]
      for (const item of navByRole[role]) {
        expect(item.to === prefix || item.to.startsWith(`${prefix}/`)).toBe(true)
      }
    }
  })
})
