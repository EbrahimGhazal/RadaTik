import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

/** Consistent list row — used in team lists, collection points, activity feeds. */
export function ListRow({
  title,
  subtitle,
  trailing,
  className,
}: {
  title: ReactNode
  subtitle?: ReactNode
  trailing?: ReactNode
  className?: string
}) {
  return (
    <li
      className={cn(
        'flex items-center justify-between gap-3 rounded-xl border border-rt-border bg-rt-neutral-bg/20 px-3.5 py-3 text-sm',
        'transition-colors hover:border-rt-primary/25 hover:bg-rt-neutral-bg/40',
        className,
      )}
    >
      <div className="min-w-0">
        <p className="font-medium text-rt-neutral-text">{title}</p>
        {subtitle ? <p className="mt-0.5 text-xs text-rt-neutral-mid">{subtitle}</p> : null}
      </div>
      {trailing ? <div className="shrink-0">{trailing}</div> : null}
    </li>
  )
}

export function ListStack({ children, className }: { children: ReactNode; className?: string }) {
  return <ul className={cn('space-y-2', className)}>{children}</ul>
}

export function KeyValueList({
  items,
  className,
}: {
  items: { label: string; value: ReactNode; valueClassName?: string }[]
  className?: string
}) {
  return (
    <ul className={cn('space-y-2.5 text-sm', className)}>
      {items.map((item) => (
        <li key={item.label} className="flex items-center justify-between gap-3">
          <span className="text-rt-neutral-mid">{item.label}</span>
          <span className={cn('font-medium tabular-nums text-rt-neutral-text', item.valueClassName)}>
            {item.value}
          </span>
        </li>
      ))}
    </ul>
  )
}
