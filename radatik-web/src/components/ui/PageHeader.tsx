import type { ReactNode } from 'react'
import { ChevronRight } from 'lucide-react'
import { Link } from 'react-router-dom'
import { cn } from '../../lib/cn'

export function PageHeader({
  title,
  description,
  breadcrumbs,
  actions,
  className,
}: {
  title: ReactNode
  description?: ReactNode
  breadcrumbs?: { label: string; to?: string }[]
  actions?: ReactNode
  className?: string
}) {
  return (
    <header className={cn('space-y-3', className)}>
      {breadcrumbs && breadcrumbs.length > 0 ? (
        <nav aria-label="Breadcrumb" className="flex flex-wrap items-center gap-1 text-xs text-rt-neutral-mid">
          {breadcrumbs.map((crumb, i) => {
            const isLast = i === breadcrumbs.length - 1
            return (
              <span key={`${crumb.label}-${i}`} className="inline-flex items-center gap-1">
                {i > 0 ? <ChevronRight className="size-3 opacity-50 rtl:rotate-180" aria-hidden /> : null}
                {crumb.to && !isLast ? (
                  <Link to={crumb.to} className="transition-colors hover:text-rt-primary">
                    {crumb.label}
                  </Link>
                ) : (
                  <span className={cn(isLast && 'font-medium text-rt-neutral-text')}>{crumb.label}</span>
                )}
              </span>
            )
          })}
        </nav>
      ) : null}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div className="min-w-0">
          <h1 className="text-xl font-bold tracking-tight text-rt-neutral-text sm:text-2xl">{title}</h1>
          {description ? <p className="mt-1.5 max-w-2xl text-sm text-rt-neutral-mid">{description}</p> : null}
        </div>
        {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </header>
  )
}
