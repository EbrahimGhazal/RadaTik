import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { NavItem } from '../navigation/navConfig'
import { cn } from '../lib/cn'

const HOME_PATHS = new Set([
  '/admin',
  '/employee',
  '/manager',
  '/client',
  '/collection',
])

/** Bottom navigation — primary items only (touch-friendly). */
export function MobileNav({ items }: { items: NavItem[] }) {
  const { t } = useTranslation()
  const primary = items.filter((i) => i.primary).slice(0, 5)

  return (
    <nav className="fixed bottom-0 inset-x-0 z-30 flex border-t border-rt-border bg-rt-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur md:hidden">
      {primary.map((item) => (
        <NavLink
          key={item.to}
          to={item.to}
          end={HOME_PATHS.has(item.to)}
          className={({ isActive }) =>
            cn(
              'flex flex-1 flex-col items-center gap-1 px-2 py-2 text-[10px] font-medium',
              isActive ? 'text-rt-primary' : 'text-rt-neutral-mid',
            )
          }
        >
          <item.icon className="size-5" />
          <span className="truncate">{t(item.labelKey)}</span>
        </NavLink>
      ))}
    </nav>
  )
}
