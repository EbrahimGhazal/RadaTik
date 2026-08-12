import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

type Variant = 'info' | 'success' | 'warning' | 'danger'

const styles: Record<Variant, string> = {
  info: 'border-rt-primary/25 bg-rt-primary-soft/40 text-rt-neutral-text',
  success: 'border-rt-green/30 bg-rt-green/10 text-rt-green',
  warning: 'border-rt-accent-orange/30 bg-rt-accent-orange/10 text-rt-accent-orange',
  danger: 'border-rt-danger/30 bg-rt-danger/10 text-rt-danger',
}

export function Alert({
  variant = 'info',
  children,
  className,
}: {
  variant?: Variant
  children: ReactNode
  className?: string
}) {
  return (
    <p className={cn('rounded-xl border px-3.5 py-2.5 text-sm', styles[variant], className)} role="status">
      {children}
    </p>
  )
}
