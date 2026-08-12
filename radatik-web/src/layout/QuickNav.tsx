import { useEffect, useMemo, useRef, useState } from 'react'
import { NavLink, useNavigate } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Search, X, Command } from 'lucide-react'
import type { NavItem } from '../navigation/navConfig'
import { cn } from '../lib/cn'

export function QuickNav({ items, open, onClose }: { items: NavItem[]; open: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const [q, setQ] = useState('')
  const inputRef = useRef<HTMLInputElement>(null)

  const filtered = useMemo(() => {
    const needle = q.trim().toLowerCase()
    if (!needle) return items
    return items.filter((item) => t(item.labelKey).toLowerCase().includes(needle))
  }, [items, q, t])

  useEffect(() => {
    if (!open) {
      setQ('')
      return
    }
    const id = window.setTimeout(() => inputRef.current?.focus(), 50)
    return () => window.clearTimeout(id)
  }, [open])

  useEffect(() => {
    if (!open) return
    const onKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose()
    }
    window.addEventListener('keydown', onKey)
    return () => window.removeEventListener('keydown', onKey)
  }, [open, onClose])

  if (!open) return null

  return (
    <div className="fixed inset-0 z-[100] flex items-start justify-center p-4 pt-[12vh] sm:p-6">
      <button
        type="button"
        className="absolute inset-0 bg-black/50 backdrop-blur-sm"
        aria-label={t('common.close')}
        onClick={onClose}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-label={t('quickNav.title')}
        className="relative w-full max-w-lg overflow-hidden rounded-2xl border border-rt-border bg-rt-surface shadow-[var(--shadow-rt-lg)] dark:shadow-[var(--shadow-rt-lg),var(--shadow-rt-glow)]"
      >
        <div className="flex items-center gap-3 border-b border-rt-border px-4 py-3">
          <Search className="size-5 shrink-0 text-rt-neutral-mid" aria-hidden />
          <input
            ref={inputRef}
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder={t('quickNav.placeholder')}
            className="min-w-0 flex-1 bg-transparent text-sm text-rt-neutral-text outline-none placeholder:text-rt-neutral-mid"
          />
          <kbd className="hidden rounded-md border border-rt-border bg-rt-neutral-bg px-1.5 py-0.5 text-[10px] font-medium text-rt-neutral-mid sm:inline">
            Esc
          </kbd>
          <button
            type="button"
            className="rounded-lg p-1.5 text-rt-neutral-mid hover:bg-rt-neutral-bg hover:text-rt-neutral-text"
            onClick={onClose}
            aria-label={t('common.close')}
          >
            <X className="size-4" />
          </button>
        </div>
        <ul className="rt-scrollbar max-h-[min(60vh,420px)] overflow-y-auto p-2">
          {filtered.length === 0 ? (
            <li className="px-3 py-8 text-center text-sm text-rt-neutral-mid">{t('quickNav.noResults')}</li>
          ) : (
            filtered.map((item) => (
              <li key={item.to}>
                <button
                  type="button"
                  className="flex w-full items-center gap-3 rounded-xl px-3 py-2.5 text-start text-sm transition-colors hover:bg-rt-neutral-bg"
                  onClick={() => {
                    navigate(item.to)
                    onClose()
                  }}
                >
                  <span className="flex size-9 items-center justify-center rounded-lg bg-rt-primary-soft text-rt-primary">
                    <item.icon className="size-4" aria-hidden />
                  </span>
                  <span className="font-medium text-rt-neutral-text">{t(item.labelKey)}</span>
                </button>
              </li>
            ))
          )}
        </ul>
        <div className="flex items-center gap-2 border-t border-rt-border bg-rt-neutral-bg/50 px-4 py-2.5 text-[11px] text-rt-neutral-mid">
          <Command className="size-3.5" aria-hidden />
          <span>{t('quickNav.hint')}</span>
        </div>
      </div>
    </div>
  )
}

/** Global ⌘K / Ctrl+K listener */
export function useQuickNavShortcut(onOpen: () => void) {
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 'k') {
        e.preventDefault()
        onOpen()
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [onOpen])
}

/** Compact nav link styles shared by sidebar + mobile sheet */
export function navLinkClass(isActive: boolean) {
  return cn(
    'flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm transition-all duration-200',
    isActive
      ? 'rt-nav-active'
      : 'text-rt-neutral-mid hover:bg-rt-neutral-bg/80 hover:text-rt-neutral-text',
  )
}

export function NavItemLink({
  item,
  label,
  end,
  onClick,
  compact,
}: {
  item: NavItem
  label: string
  end?: boolean
  onClick?: () => void
  compact?: boolean
}) {
  return (
    <NavLink to={item.to} end={end} onClick={onClick} className={({ isActive }) => navLinkClass(isActive)}>
      <item.icon className={cn('shrink-0 opacity-90', compact ? 'size-5' : 'size-4')} aria-hidden />
      {!compact ? <span className="truncate">{label}</span> : null}
    </NavLink>
  )
}
