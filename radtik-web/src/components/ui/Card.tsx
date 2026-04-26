import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

export function Card({
  title,
  children,
  className,
  actions,
  flush,
}: {
  title?: ReactNode
  children: ReactNode
  className?: string
  actions?: ReactNode
  /** Remove inner padding (full-bleed charts, custom layouts). */
  flush?: boolean
}) {
  return (
    <section
      className={cn(
        'rounded-xl border border-rt-border bg-rt-surface shadow-sm',
        className,
      )}
    >
      {(title || actions) && (
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-rt-border px-4 py-3">
          {title ? (
            <h2 className="text-sm font-semibold text-rt-neutral-text">{title}</h2>
          ) : (
            <span />
          )}
          {actions}
        </header>
      )}
      <div className={cn(!flush && 'p-4')}>{children}</div>
    </section>
  )
}
