import { cn } from '../../lib/cn'

export function TabGroup<T extends string>({
  value,
  onChange,
  tabs,
  className,
}: {
  value: T
  onChange: (v: T) => void
  tabs: { id: T; label: string }[]
  className?: string
}) {
  return (
    <div
      role="tablist"
      className={cn(
        'rt-segment flex flex-wrap gap-1 p-1',
        className,
      )}
    >
      {tabs.map((tab) => {
        const active = tab.id === value
        return (
          <button
            key={tab.id}
            type="button"
            role="tab"
            aria-selected={active}
            onClick={() => onChange(tab.id)}
            className={cn(
              'rounded-lg px-3 py-2 text-sm font-medium transition-all duration-200 sm:px-4',
              active
                ? 'bg-rt-surface text-rt-primary shadow-[var(--shadow-rt-sm)] dark:text-rt-primary'
                : 'text-rt-neutral-mid hover:text-rt-neutral-text',
            )}
          >
            {tab.label}
          </button>
        )
      })}
    </div>
  )
}
