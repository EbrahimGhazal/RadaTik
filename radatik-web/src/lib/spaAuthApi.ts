import type { AuthUser, UserRole } from '../types'
import { mvcBaseUrl } from './mvcBaseUrl'

export function spaAuthLoginUrl(): string {
  return `${mvcBaseUrl()}/api/spa-auth/login`
}

export function spaAuthLogoutUrl(): string {
  return `${mvcBaseUrl()}/api/spa-auth/logout`
}

export function spaAuthMeUrl(): string {
  return `${mvcBaseUrl()}/api/spa-auth/me`
}

export type SpaLoginResult =
  | {
      ok: true
      user: { id: string; email: string; fullName: string; role: UserRole }
    }
  | { ok: false; message?: string }

export async function loginWithDatabase(userName: string, password: string): Promise<SpaLoginResult> {
  let res: Response
  try {
    res = await fetch(spaAuthLoginUrl(), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
      credentials: 'include',
      body: JSON.stringify({ userName, password }),
    })
  } catch {
    return { ok: false, message: 'networkError' }
  }

  let raw: unknown
  try {
    raw = await res.json()
  } catch {
    return { ok: false, message: 'badResponse' }
  }

  if (
    raw &&
    typeof raw === 'object' &&
    'ok' in raw &&
    (raw as { ok: unknown }).ok === true &&
    'user' in raw &&
    typeof (raw as { user: unknown }).user === 'object' &&
    (raw as { user: { id?: unknown } }).user !== null
  ) {
    const u = (raw as { user: Record<string, unknown> }).user
    const id = typeof u.id === 'string' ? u.id : ''
    const email = typeof u.email === 'string' ? u.email : ''
    const fullName = typeof u.fullName === 'string' ? u.fullName : ''
    const role = u.role as UserRole
    if (id && role) {
      return { ok: true, user: { id, email, fullName, role } }
    }
  }

  const message =
    raw && typeof raw === 'object' && 'message' in raw && typeof (raw as { message: unknown }).message === 'string'
      ? (raw as { message: string }).message
      : undefined

  if (!res.ok && message) {
    return { ok: false, message }
  }

  return { ok: false, message: message ?? (res.status === 401 ? 'invalidCredentials' : 'badResponse') }
}

export function mapLoginUser(data: { id: string; email: string; fullName: string; role: UserRole }): AuthUser {
  return {
    id: data.id,
    email: data.email,
    fullName: data.fullName,
    role: data.role,
  }
}

export async function logoutSpaSession(): Promise<void> {
  try {
    await fetch(spaAuthLogoutUrl(), {
      method: 'POST',
      credentials: 'include',
    })
  } catch (error) {
    console.warn('Failed to call SPA logout endpoint.', error)
  }
}

export async function getCurrentSpaUser(): Promise<AuthUser | null> {
  try {
    const res = await fetch(spaAuthMeUrl(), {
      method: 'GET',
      headers: { Accept: 'application/json' },
      credentials: 'include',
    })

    if (!res.ok) {
      return null
    }

    const raw = (await res.json()) as unknown
    if (
      raw &&
      typeof raw === 'object' &&
      'ok' in raw &&
      (raw as { ok: unknown }).ok === true &&
      'user' in raw
    ) {
      const mapped = mapLoginUser((raw as { user: { id: string; email: string; fullName: string; role: UserRole } }).user)
      return mapped
    }
  } catch (error) {
    console.warn('Failed to validate current SPA session.', error)
  }

  return null
}
