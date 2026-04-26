import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { NavItem } from '../navigation/navConfig'
import { cn } from '../lib/cn'
import { X } from 'lucide-react'

const ROLE_HOME_PATHS = new Set([
  '/admin',
  '/employee',
  '/manager',
  '/client',
  '/collection',
])

export function Sidebar({
  items,
  open,
  onClose,
}: {
  items: NavItem[]
  open: boolean
  onClose: () => void
}) {
  const { t } = useTranslation()

  return (
    <>
      <aside
        className={cn(
          'fixed inset-y-0 z-50 w-64 border-e border-rt-border bg-rt-surface transition-transform duration-200 md:static',
          'start-0 md:z-0',
          /* إخفاء الساحب على الموبايل فقط؛ على md+ يجب أن تظل translate-x-0 وإلا rtl:translate-x-full تطغى على md:translate-x-0 */
          open
            ? 'max-md:translate-x-0'
            : 'max-md:-translate-x-full max-md:rtl:translate-x-full',
          'md:translate-x-0',
        )}
      >
        <div className="flex h-14 items-center justify-between px-4 md:hidden">
          <span className="text-sm font-semibold text-rt-neutral-text">{t('app.name')}</span>
          <button
            type="button"
            className="rounded-lg border border-rt-border p-2"
            aria-label={t('common.close')}
            onClick={onClose}
          >
            <X className="size-4" />
          </button>
        </div>
        <nav className="flex flex-col gap-1 p-3 md:pt-6">
          {items.map((item) => (
            <NavLink
              key={item.to}
              to={item.to}
              end={ROLE_HOME_PATHS.has(item.to)}
              onClick={onClose}
              className={({ isActive }) =>
                cn(
                  'flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-rt-primary/10 text-rt-primary'
                    : 'text-rt-neutral-mid hover:bg-rt-neutral-bg hover:text-rt-neutral-text',
                )
              }
            >
              <item.icon className="size-4 shrink-0 opacity-90" />
              <span>{t(item.labelKey)}</span>
            </NavLink>
          ))}
        </nav>
      </aside>
      {open ? (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-black/40 md:hidden"
          aria-label={t('common.close')}
          onClick={onClose}
        />
      ) : null}
    </>
  )
}
