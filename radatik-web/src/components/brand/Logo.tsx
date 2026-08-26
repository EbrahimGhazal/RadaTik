import { useUiStore } from '../../store/uiStore'
import { cn } from '../../lib/cn'

interface LogoProps {
  className?: string
  /** Show full wordmark logo; when false shows monogram mark only */
  showWordmark?: boolean
  /** Smaller footprint for mobile header */
  compact?: boolean
}

function Monogram() {
  return (
    <>
      <g fill="none" strokeLinecap="butt" strokeLinejoin="round">
        <path className="rt-logo-orange" strokeWidth="12" d="M46 14 H88 M74 14 V80" />
        <path className="rt-logo-orange-trace" strokeWidth="1.8" d="M52 14 H82 M74 20 V74" />
        <circle className="rt-logo-orange-trace" cx="52" cy="14" r="2.2" />
        <circle className="rt-logo-orange-trace" cx="82" cy="14" r="2.2" />
        <circle className="rt-logo-orange-trace" cx="74" cy="74" r="2.2" />
      </g>
      <g fill="none" strokeLinecap="butt" strokeLinejoin="round">
        <path
          className="rt-logo-navy"
          strokeWidth="12"
          d="M16 80 V14 H52 V14 H52 C66 14 66 14 66 31 C66 48 66 48 52 48 H16 M48 48 L70 80"
        />
        <path
          className="rt-logo-navy-trace"
          strokeWidth="1.8"
          d="M16 74 V20 H50 C58 20 58 20 58 31 C58 42 58 42 50 42 H22 M50 50 L64 72"
        />
        <circle className="rt-logo-navy-trace" cx="16" cy="74" r="2.2" />
        <circle className="rt-logo-navy-trace" cx="16" cy="20" r="2.2" />
        <circle className="rt-logo-navy-trace" cx="50" cy="20" r="2.2" />
        <circle className="rt-logo-navy-trace" cx="22" cy="42" r="2.2" />
        <circle className="rt-logo-navy-trace" cx="64" cy="72" r="2.2" />
      </g>
    </>
  )
}

/** RADATIK vector brand — transparent SVG that follows surface + theme colors. */
export function Logo({ className, showWordmark = true, compact = false }: LogoProps) {
  const theme = useUiStore((s) => s.theme)
  const height = compact ? 32 : showWordmark ? 40 : 36

  return (
    <div
      className={cn('rt-brand-block inline-flex items-center bg-transparent', className)}
      data-theme={theme}
      dir="ltr"
      role="img"
      aria-label="RADATIK technology L.L.C"
    >
      {showWordmark ? (
        <svg
          className="rt-logo-svg block w-auto max-w-full"
          viewBox="0 0 330 92"
          height={height}
          aria-hidden
          focusable="false"
        >
          <Monogram />
          <text
            className="rt-logo-word"
            x="108"
            y="44"
            fontFamily="Poppins, Arial, Helvetica, sans-serif"
            fontSize="30"
            fontWeight="700"
            letterSpacing="0.045em"
          >
            RADATIK
          </text>
          <text
            className="rt-logo-sub"
            x="110"
            y="68"
            fontFamily="Poppins, Arial, Helvetica, sans-serif"
            fontSize="13"
            fontWeight="500"
            letterSpacing="0.02em"
          >
            technology L.L.C
          </text>
        </svg>
      ) : (
        <svg
          className="rt-logo-svg block w-auto max-w-full"
          viewBox="0 0 100 92"
          height={height}
          aria-hidden
          focusable="false"
        >
          <Monogram />
        </svg>
      )}
    </div>
  )
}
