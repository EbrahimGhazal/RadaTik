import { cn } from '../../lib/cn'

const styles: Record<string, string> = {
  paid: 'bg-rt-green/15 text-rt-green ring-1 ring-rt-green/30',
  pending: 'bg-rt-accent-orange/15 text-rt-accent-orange ring-1 ring-rt-accent-orange/30',
  done: 'bg-rt-green/15 text-rt-green ring-1 ring-rt-green/30',
  in_progress: 'bg-rt-secondary/15 text-rt-secondary ring-1 ring-rt-secondary/30',
  active: 'bg-rt-green/15 text-rt-green ring-1 ring-rt-green/30',
  suspended: 'bg-rt-danger/15 text-rt-danger ring-1 ring-rt-danger/30',
  online: 'bg-rt-green/15 text-rt-green ring-1 ring-rt-green/30',
  sync: 'bg-rt-secondary/15 text-rt-secondary ring-1 ring-rt-secondary/30',
  delayed: 'bg-rt-accent-orange/15 text-rt-accent-orange ring-1 ring-rt-accent-orange/30',
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
