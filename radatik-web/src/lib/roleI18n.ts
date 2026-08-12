import type { TFunction } from 'i18next'
import { ROLE_LABEL_KEY, type UserRole } from '../types'

export function translateRole(t: TFunction, role: string): string {
  const key = ROLE_LABEL_KEY[role as UserRole]
  return key ? t(key) : role
}
