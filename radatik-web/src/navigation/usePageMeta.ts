import { useMemo } from 'react'
import { useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { navByRole } from './navConfig'
import { ROLE_ROUTE, type UserRole } from '../types'

export function usePageMeta(role: UserRole | undefined) {
  const { pathname } = useLocation()
  const { t } = useTranslation()

  return useMemo(() => {
    if (!role) return { title: '', breadcrumbs: [] as { label: string; to?: string }[] }

    const items = navByRole[role]
    const home = ROLE_ROUTE[role]
    const match = items.find((item) => item.to === pathname) ?? items.find((item) => item.to === home)
    const title = match ? t(match.labelKey) : t('nav.dashboard')

    const breadcrumbs = [
      { label: t('nav.dashboard'), to: home },
      ...(pathname !== home ? [{ label: title }] : []),
    ]

    return { title, breadcrumbs, currentPath: pathname }
  }, [role, pathname, t])
}
