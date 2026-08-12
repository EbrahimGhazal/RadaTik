import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

export function Card({
  title,
  children,
  className,
  actions,
  flush,
  elevated,
}: {
  title?: ReactNode
  children: ReactNode
  className?: string
  actions?: ReactNode
  /** Remove inner padding (full-bleed charts, custom layouts). */
  flush?: boolean
  /** Slightly raised surface for nested panels in dark mode. */
  elevated?: boolean
}) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-2xl border border-rt-border shadow-[var(--shadow-rt-sm)] transition-shadow duration-300',
        elevated ? 'bg-rt-elevated' : 'bg-rt-surface',
        'hover:shadow-[var(--shadow-rt-md)] dark:hover:shadow-[var(--shadow-rt-md),var(--shadow-rt-glow)]',
        className,
      )}
    >
      {(title || actions) && (
        <header className="flex flex-wrap items-center justify-between gap-2 border-b border-rt-border bg-rt-neutral-bg/30 px-4 py-3.5 sm:px-5">
          {title ? (
            <h2 className="text-sm font-semibold tracking-tight text-rt-neutral-text">{title}</h2>
          ) : (
            <span />
          )}
          {actions}
        </header>
      )}
      <div className={cn(!flush && 'p-4 sm:p-5')}>{children}</div>
    </section>
  )
}
