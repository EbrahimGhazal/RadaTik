interface LogoProps {
  className?: string
  /** Show full wordmark logo; when false shows monogram mark only */
  showWordmark?: boolean
  /** Smaller footprint for mobile header */
  compact?: boolean
}

const BRAND = `${import.meta.env.BASE_URL}brand`
const LIGHT_LOGO = `${BRAND}/radatik-logo-light.png`
const DARK_LOGO = `${BRAND}/radatik-logo-dark.png`
const LIGHT_MARK = `${BRAND}/radatik-mark-light.png`
const DARK_MARK = `${BRAND}/radatik-mark-dark.png`

/**
 * RADATIK official brand mark (full logo or monogram), with light/dark assets.
 */
export function Logo({ className, showWordmark = true, compact = false }: LogoProps) {
  const height = compact ? 32 : showWordmark ? 40 : 36
  const alt = showWordmark ? 'RADATIK technology L.L.C' : 'RADATIK'
  const lightSrc = showWordmark ? LIGHT_LOGO : LIGHT_MARK
  const darkSrc = showWordmark ? DARK_LOGO : DARK_MARK
  const imgClass = 'w-auto max-w-full object-contain'

  return (
    <div className={['inline-flex items-center', className].filter(Boolean).join(' ')}>
      <img
        src={lightSrc}
        alt={alt}
        height={height}
        className={`block ${imgClass} dark:hidden`}
        style={{ height }}
        draggable={false}
      />
      <img
        src={darkSrc}
        alt=""
        aria-hidden
        height={height}
        className={`hidden ${imgClass} dark:block`}
        style={{ height }}
        draggable={false}
      />
    </div>
  )
}
