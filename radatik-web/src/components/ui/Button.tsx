import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { cn } from '../../lib/cn'

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger'
type Size = 'sm' | 'md' | 'lg'

const variantStyles: Record<Variant, string> = {
  primary:
    'bg-rt-primary text-white shadow-md hover:brightness-110 active:scale-[0.98] dark:text-slate-950 dark:font-semibold dark:shadow-[var(--shadow-rt-glow)]',
  secondary:
    'border border-rt-border bg-rt-surface text-rt-neutral-text hover:bg-rt-neutral-bg active:scale-[0.98]',
  ghost: 'text-rt-neutral-text hover:bg-rt-neutral-bg active:scale-[0.98]',
  danger:
    'bg-rt-danger text-white shadow-md hover:brightness-110 active:scale-[0.98]',
}

const sizeStyles: Record<Size, string> = {
  sm: 'px-3 py-1.5 text-xs gap-1.5 rounded-lg',
  md: 'px-4 py-2.5 text-sm gap-2 rounded-xl',
  lg: 'px-5 py-3 text-base gap-2.5 rounded-xl',
}

export function Button({
  variant = 'primary',
  size = 'md',
  className,
  children,
  leftIcon,
  rightIcon,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: Variant
  size?: Size
  leftIcon?: ReactNode
  rightIcon?: ReactNode
}) {
  return (
    <button
      type="button"
      className={cn(
        'inline-flex items-center justify-center font-medium transition-all duration-200',
        'disabled:pointer-events-none disabled:opacity-50',
        variantStyles[variant],
        sizeStyles[size],
        className,
      )}
      {...props}
    >
      {leftIcon}
      {children}
      {rightIcon}
    </button>
  )
}
