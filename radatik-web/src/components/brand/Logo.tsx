interface LogoProps {
  className?: string
  /** Show full wordmark logo; when false shows monogram mark only */
  showWordmark?: boolean
  /** Smaller footprint for mobile header */
  compact?: boolean
}

/**
 * RADATIK official brand mark (full logo or monogram).
 */
export function Logo({ className, showWordmark = true, compact = false }: LogoProps) {
  const height = compact ? 32 : showWordmark ? 40 : 36
  const src = showWordmark ? '/images/brand/radatik-logo.png' : '/images/brand/radatik-mark.png'
  const alt = showWordmark ? 'RADATIK technology L.L.C' : 'RADATIK'

  return (
    <div className={['inline-flex items-center', className].filter(Boolean).join(' ')}>
      <img
        src={src}
        alt={alt}
        height={height}
        className="block w-auto max-w-full object-contain"
        style={{ height }}
        draggable={false}
      />
    </div>
  )
}
