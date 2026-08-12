import type { LucideIcon } from 'lucide-react'
import { cn } from '../../lib/cn'

export function StatCard({
  label,
  value,
  icon: Icon,
  trend,
  className,
}: {
  label: string
  value: string | number
  icon?: LucideIcon
  trend?: { label: string; positive?: boolean }
  className?: string
}) {
  return (
    <article
      className={cn(
        'group relative overflow-hidden rounded-2xl border border-rt-border bg-rt-surface p-5',
        'shadow-[var(--shadow-rt-sm)] transition-all duration-300 hover:border-rt-primary/30 hover:shadow-[var(--shadow-rt-md)]',
        className,
      )}
    >
      <div
        className="pointer-events-none absolute -end-6 -top-6 size-24 rounded-full bg-rt-primary/5 transition-transform duration-500 group-hover:scale-110"
        aria-hidden
      />
      <div className="relative flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <p className="text-xs font-medium uppercase tracking-wider text-rt-neutral-mid">{label}</p>
          <p className="mt-2 text-3xl font-bold tabular-nums tracking-tight text-rt-neutral-text">
            {typeof value === 'number' ? value.toLocaleString() : value}
          </p>
          {trend ? (
            <p
              className={cn(
                'mt-2 text-xs font-medium',
                trend.positive ? 'text-rt-green' : 'text-rt-accent-orange',
              )}
            >
              {trend.label}
            </p>
          ) : null}
        </div>
        {Icon ? (
          <div className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-rt-primary-soft text-rt-primary">
            <Icon className="size-5" aria-hidden />
          </div>
        ) : null}
      </div>
    </article>
  )
}
