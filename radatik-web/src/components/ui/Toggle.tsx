import { cn } from '../../lib/cn'

export function Toggle({
  checked,
  onChange,
  label,
  id,
}: {
  checked: boolean
  onChange: (next: boolean) => void
  label: string
  id?: string
}) {
  return (
    <button
      id={id}
      type="button"
      role="switch"
      aria-checked={checked}
      aria-label={label}
      onClick={() => onChange(!checked)}
      className={cn(
        'relative flex h-7 w-12 shrink-0 items-center rounded-full p-0.5 transition-colors duration-200',
        checked ? 'bg-rt-primary' : 'bg-rt-neutral-mid/35',
      )}
      style={checked ? { boxShadow: '0 0 12px color-mix(in srgb, var(--color-rt-primary) 35%, transparent)' } : undefined}
    >
      <span
        className={cn(
          'size-6 rounded-full bg-white shadow-md ring-1 ring-black/5 transition-transform duration-200',
          'dark:bg-slate-100',
          checked ? 'translate-x-5 rtl:-translate-x-5' : 'translate-x-0',
        )}
      />
    </button>
  )
}

export function SettingsRow({
  label,
  description,
  children,
}: {
  label: string
  description?: string
  children: React.ReactNode
}) {
  return (
    <li className="flex items-center justify-between gap-4 rounded-xl border border-rt-border bg-rt-neutral-bg/20 px-4 py-3.5 transition-colors hover:border-rt-primary/20 sm:px-5">
      <div className="min-w-0">
        <span className="text-sm font-medium text-rt-neutral-text">{label}</span>
        {description ? <p className="mt-0.5 text-xs text-rt-neutral-mid">{description}</p> : null}
      </div>
      {children}
    </li>
  )
}
