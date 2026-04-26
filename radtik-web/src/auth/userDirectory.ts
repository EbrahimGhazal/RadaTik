import type { AuthUser, UserRole } from '../types'

const LS_KEY = 'radtik-registered-users'

/** Starter accounts for quick demos (any password works in mock auth). */
export const SEED_USERS: AuthUser[] = [
  {
    id: 'seed-admin',
    email: 'admin@radtik.dev',
    fullName: 'Noor Admin',
    role: 'system_admin',
  },
  {
    id: 'seed-employee',
    email: 'ops@radtik.dev',
    fullName: 'Sam Operator',
    role: 'employee',
  },
  {
    id: 'seed-manager',
    email: 'manager@radtik.dev',
    fullName: 'Jean Manager',
    role: 'company_manager',
  },
  {
    id: 'seed-client',
    email: 'client@radtik.dev',
    fullName: 'River Client',
    role: 'client',
  },
  {
    id: 'seed-collection',
    email: 'cash@radtik.dev',
    fullName: 'Mina Booth',
    role: 'collection_point',
  },
]

function readRegistered(): AuthUser[] {
  try {
    const raw = localStorage.getItem(LS_KEY)
    if (!raw) return []
    return JSON.parse(raw) as AuthUser[]
  } catch {
    return []
  }
}

export function saveRegistered(user: AuthUser) {
  const all = readRegistered()
  all.push(user)
  localStorage.setItem(LS_KEY, JSON.stringify(all))
}

export function findUserByEmail(email: string): AuthUser | undefined {
  const normalized = email.trim().toLowerCase()
  const merged = [...readRegistered(), ...SEED_USERS]
  return merged.find((u) => u.email.toLowerCase() === normalized)
}

/** يسمح بالدخول بـ "admin" بدل البريد الكامل (متوافق مع عادات المطورين المحلية). */
const LOGIN_EMAIL_ALIASES: Record<string, string> = {
  admin: 'admin@radtik.dev',
  administrator: 'admin@radtik.dev',
  ops: 'ops@radtik.dev',
  manager: 'manager@radtik.dev',
  client: 'client@radtik.dev',
  cash: 'cash@radtik.dev',
}

export function resolveUserForLogin(emailInput: string): AuthUser | undefined {
  const raw = emailInput.trim().toLowerCase()
  const resolved = LOGIN_EMAIL_ALIASES[raw] ?? emailInput.trim()
  return findUserByEmail(resolved)
}

/**
 * كلمات مرور تجريبية اختيارية لحسابات الـ seed (باقي الحسابات: أي كلمة 4 أحرف فأكثر).
 * محاذاة مع حساب مشرف محلي شائع.
 */
const SEED_PASSWORD_BY_EMAIL: Record<string, string> = {
  'admin@radtik.dev': 'admin@123Gh',
}

export function verifyMockPassword(user: AuthUser, password: string): boolean {
  const minLen = 4
  if (password.length < minLen) return false
  const rule = SEED_PASSWORD_BY_EMAIL[user.email.toLowerCase()]
  if (rule === undefined) return true
  return password === rule
}

/** Map signup form roles to `UserRole` keys. */
export const SIGNUP_ROLE_OPTIONS: { value: UserRole; labelKey: string }[] = [
  { value: 'system_admin', labelKey: 'roles.systemAdmin' },
  { value: 'employee', labelKey: 'roles.employee' },
  { value: 'company_manager', labelKey: 'roles.companyManager' },
  { value: 'client', labelKey: 'roles.client' },
  { value: 'collection_point', labelKey: 'roles.collectionPoint' },
]
