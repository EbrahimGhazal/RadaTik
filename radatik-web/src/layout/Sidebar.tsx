import { useTranslation } from 'react-i18next'
import { LayoutGrid, X } from 'lucide-react'
import { Logo } from '../components/brand/Logo'
import type { NavItem } from '../navigation/navConfig'
import { cn } from '../lib/cn'
import { NavItemLink } from './QuickNav'

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
  onQuickNavOpen,
}: {
  items: NavItem[]
  open: boolean
  onClose: () => void
  onQuickNavOpen?: () => void
}) {
  const { t } = useTranslation()

  return (
    <>
      <aside
        className={cn(
          'fixed inset-y-0 z-50 flex w-[min(18rem,85vw)] flex-col border-e border-rt-border bg-rt-surface/95 backdrop-blur-xl transition-transform duration-300',
          'start-0 md:static md:z-0 md:w-56 md:translate-x-0 lg:w-64',
          open
            ? 'max-md:translate-x-0'
            : 'max-md:-translate-x-full max-md:rtl:translate-x-full',
        )}
      >
        <div className="flex h-14 shrink-0 items-center justify-between border-b border-rt-border px-4 md:hidden">
          <span className="text-sm font-semibold text-rt-neutral-text">{t('app.name')}</span>
          <button
            type="button"
            className="rounded-xl border border-rt-border p-2 transition-colors hover:bg-rt-neutral-bg"
            aria-label={t('common.close')}
            onClick={onClose}
          >
            <X className="size-4" />
          </button>
        </div>

        <div className="hidden h-16 shrink-0 items-center border-b border-rt-border px-4 md:flex">
          <Logo compact />
        </div>

        {onQuickNavOpen ? (
          <div className="hidden border-b border-rt-border p-3 md:block">
            <button
              type="button"
              onClick={onQuickNavOpen}
              className="flex w-full items-center gap-3 rounded-xl border border-rt-border bg-rt-neutral-bg/60 px-3 py-2.5 text-sm text-rt-neutral-mid transition-colors hover:border-rt-primary/30 hover:text-rt-neutral-text"
            >
              <LayoutGrid className="size-4 shrink-0" aria-hidden />
              <span className="flex-1 text-start">{t('quickNav.open')}</span>
              <kbd className="rounded-md border border-rt-border bg-rt-surface px-1.5 py-0.5 text-[10px] font-medium">
                ⌘K
              </kbd>
            </button>
          </div>
        ) : null}

        <nav className="rt-scrollbar flex flex-1 flex-col gap-1 overflow-y-auto p-3 md:pt-4">
          {items.map((item) => (
            <NavItemLink
              key={item.to}
              item={item}
              label={t(item.labelKey)}
              end={ROLE_HOME_PATHS.has(item.to)}
              onClick={onClose}
            />
          ))}
        </nav>

        <div className="hidden border-t border-rt-border p-3 md:block">
          <p className="text-center text-[10px] leading-relaxed text-rt-neutral-mid">{t('sidebar.hint')}</p>
        </div>
      </aside>
      {open ? (
        <button
          type="button"
          className="fixed inset-0 z-40 bg-black/50 backdrop-blur-[2px] md:hidden"
          aria-label={t('common.close')}
          onClick={onClose}
        />
      ) : null}
    </>
  )
}
