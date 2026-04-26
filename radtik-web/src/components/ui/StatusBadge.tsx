import { cn } from '../../lib/cn'

const styles: Record<string, string> = {
  paid: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 ring-1 ring-emerald-500/30',
  pending: 'bg-amber-500/15 text-amber-800 dark:text-amber-200 ring-1 ring-amber-500/30',
  done: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 ring-1 ring-emerald-500/30',
  in_progress: 'bg-sky-500/15 text-sky-800 dark:text-sky-200 ring-1 ring-sky-500/30',
  active: 'bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 ring-1 ring-emerald-500/30',
  suspended: 'bg-rose-500/15 text-rose-800 dark:text-rose-200 ring-1 ring-rose-500/30',
}

export function StatusBadge({
  label,
  code,
}: {
  label: string
  code: string
}) {
  const c = styles[code] ?? 'bg-rt-neutral-bg text-rt-neutral-mid ring-1 ring-rt-border'
  return (
    <span
      className={cn(
        'inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium',
        c,
      )}
    >
      {label}
    </span>
  )
}
