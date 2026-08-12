import type { ReactNode } from 'react'
import { Link, type LinkProps } from 'react-router-dom'
import { cn } from '../../lib/cn'

type Size = 'sm' | 'md' | 'lg'

const sizeStyles: Record<Size, string> = {
  sm: 'px-3 py-1.5 text-xs gap-1.5 rounded-lg',
  md: 'px-4 py-2.5 text-sm gap-2 rounded-xl',
  lg: 'px-5 py-3 text-base gap-2.5 rounded-xl',
}

/** Link styled as secondary button — same look across all pages/roles. */
export function LinkButton({
  to,
  children,
  className,
  size = 'md',
  leftIcon,
}: LinkProps & {
  children: ReactNode
  className?: string
  size?: Size
  leftIcon?: ReactNode
}) {
  return (
    <Link
      to={to}
      className={cn(
        'inline-flex items-center justify-center font-medium transition-all duration-200',
        'border border-rt-border bg-rt-surface text-rt-neutral-text',
        'hover:border-rt-primary/30 hover:bg-rt-primary-soft/30 hover:text-rt-primary',
        'active:scale-[0.98]',
        sizeStyles[size],
        className,
      )}
    >
      {leftIcon}
      {children}
    </Link>
  )
}
