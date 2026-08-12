import { useId } from 'react'
import { useUiStore } from '../../store/uiStore'
import { cn } from '../../lib/cn'

interface LogoProps {
  className?: string
  /** Show text wordmark beside the icon */
  showWordmark?: boolean
  /** Smaller footprint for mobile header */
  compact?: boolean
}

/**
 * RadaTik logomark: network ring, routing strokes, access glow, geometric R silhouette.
 * Colors follow light vs dark brand (blue/cyan vs neon blue/orange).
 */
export function Logo({ className, showWordmark = true, compact = false }: LogoProps) {
  const glowFilterId = `radatik-glow-${useId().replace(/:/g, '')}`
  const theme = useUiStore((s) => s.theme)
  const isDark = theme === 'dark'
  const strokeMain = isDark ? '#3B82F6' : '#2563EB'
  const strokeAccent = isDark ? '#FB923C' : '#06B6D4'
  const glow = isDark ? '#60A5FA' : '#22D3EE'

  return (
    <div className={cn('flex items-center gap-2', className)}>
      <svg
        width={compact ? 36 : 44}
        height={compact ? 36 : 44}
        viewBox="0 0 48 48"
        aria-hidden
        className="shrink-0"
      >
        <defs>
          <filter id={glowFilterId} x="-40%" y="-40%" width="180%" height="180%">
            <feGaussianBlur stdDeviation="1.2" result="b" />
            <feMerge>
              <feMergeNode in="b" />
              <feMergeNode in="SourceGraphic" />
            </feMerge>
          </filter>
        </defs>
        {/* Outer ring — network */}
        <circle
          cx="24"
          cy="24"
          r="20"
          fill="none"
          stroke={strokeMain}
          strokeWidth="2"
          opacity={0.9}
        />
        {/* Crossing routes */}
        <path
          d="M12 30 L24 18 L36 30 M18 14 L30 26"
          fill="none"
          stroke={strokeAccent}
          strokeWidth="1.8"
          strokeLinecap="round"
          opacity={0.95}
        />
        {/* Letter R — geometric stem and bowl */}
        <path
          d="M17 14 V34 M17 14 H26 Q31 14 31 19 Q31 24 26 24 H17 M24 24 L31 34"
          fill="none"
          stroke={strokeMain}
          strokeWidth="2.2"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
        {/* Access point glow */}
        <circle cx="31" cy="16" r="2.4" fill={glow} filter={`url(#${glowFilterId})`} />
      </svg>
      {showWordmark ? (
        <div
          className={cn(
            'flex flex-col leading-none select-none',
            compact ? 'gap-0' : 'gap-0.5',
          )}
        >
          <span
            className={cn(
              'font-semibold tracking-tight',
              compact ? 'text-base' : 'text-lg',
              isDark ? 'text-[#3B82F6]' : 'text-[#2563EB]',
            )}
            style={{ fontFamily: 'Poppins, sans-serif' }}
          >
            R
            <span className={cn(isDark ? 'text-[#FB923C]' : 'text-[#06B6D4]')}>ada</span>
            T
            <span className="relative inline-flex items-center">
              i
              <span
                className="absolute -top-0.5 left-1/2 h-1.5 w-1.5 -translate-x-1/2 rounded-full rtl:left-1/2"
                style={{
                  background: glow,
                  boxShadow: `0 0 8px ${glow}`,
                }}
              />
            </span>
            k
          </span>
        </div>
      ) : null}
    </div>
  )
}
