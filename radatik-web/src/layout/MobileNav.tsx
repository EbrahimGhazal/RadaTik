import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { MoreHorizontal } from 'lucide-react'
import type { NavItem } from '../navigation/navConfig'
import { cn } from '../lib/cn'
import { NavItemLink } from './QuickNav'

const HOME_PATHS = new Set([
  '/admin',
  '/employee',
  '/manager',
  '/client',
  '/collection',
])

/** Bottom navigation — primary items + overflow sheet for full access on mobile. */
export function MobileNav({ items }: { items: NavItem[] }) {
  const { t } = useTranslation()
  const [moreOpen, setMoreOpen] = useState(false)
  const primary = items.filter((i) => i.primary).slice(0, 4)
  const overflow = items.filter((i) => !primary.some((p) => p.to === i.to))
  const showMore = overflow.length > 0

  return (
    <>
      <nav className="fixed bottom-0 inset-x-0 z-30 flex border-t border-rt-border bg-rt-surface/95 pb-[env(safe-area-inset-bottom)] backdrop-blur-xl md:hidden">
        {primary.map((item) => (
          <NavLink
            key={item.to}
            to={item.to}
            end={HOME_PATHS.has(item.to)}
            className={({ isActive }) =>
              cn(
                'relative flex flex-1 flex-col items-center gap-1 px-1.5 py-2.5 text-[10px] font-medium transition-colors',
                isActive ? 'rt-mobile-nav-active text-rt-primary' : 'text-rt-neutral-mid',
              )
            }
          >
            <item.icon className="size-5" aria-hidden />
            <span className="max-w-full truncate">{t(item.labelKey)}</span>
          </NavLink>
        ))}
        {showMore ? (
          <button
            type="button"
            onClick={() => setMoreOpen(true)}
            className={cn(
              'flex flex-1 flex-col items-center gap-1 px-1.5 py-2.5 text-[10px] font-medium transition-colors',
              moreOpen ? 'text-rt-primary' : 'text-rt-neutral-mid',
            )}
            aria-label={t('mobileNav.more')}
          >
            <MoreHorizontal className="size-5" aria-hidden />
            <span>{t('mobileNav.more')}</span>
          </button>
        ) : null}
      </nav>

      {moreOpen ? (
        <div className="fixed inset-0 z-[90] md:hidden">
          <button
            type="button"
            className="absolute inset-0 bg-black/50 backdrop-blur-sm"
            aria-label={t('common.close')}
            onClick={() => setMoreOpen(false)}
          />
          <div className="absolute inset-x-0 bottom-0 max-h-[70vh] overflow-hidden rounded-t-2xl border-t border-rt-border bg-rt-surface shadow-[var(--shadow-rt-lg)]">
            <div className="flex items-center justify-between border-b border-rt-border px-4 py-3">
              <h2 className="text-sm font-semibold text-rt-neutral-text">{t('mobileNav.allPages')}</h2>
              <button
                type="button"
                className="rounded-lg px-3 py-1.5 text-xs font-medium text-rt-primary"
                onClick={() => setMoreOpen(false)}
              >
                {t('common.close')}
              </button>
            </div>
            <nav className="rt-scrollbar grid gap-1 overflow-y-auto p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))]">
              {items.map((item) => (
                <NavItemLink
                  key={item.to}
                  item={item}
                  label={t(item.labelKey)}
                  end={HOME_PATHS.has(item.to)}
                  onClick={() => setMoreOpen(false)}
                />
              ))}
            </nav>
          </div>
        </div>
      ) : null}
    </>
  )
}
