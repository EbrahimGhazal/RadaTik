/**
 * Semantic surface/text tones — use instead of raw emerald/amber/rose/sky.
 * Keeps light + dark mode consistent via design tokens.
 */
export type Tone = 'success' | 'warning' | 'danger' | 'info' | 'neutral'

const surface: Record<Tone, string> = {
  success: 'border-rt-green/40 bg-rt-green/10 text-rt-green',
  warning: 'border-rt-accent-orange/40 bg-rt-accent-orange/10 text-rt-accent-orange',
  danger: 'border-rt-danger/40 bg-rt-danger/10 text-rt-danger',
  info: 'border-rt-secondary/40 bg-rt-secondary/10 text-rt-secondary',
  neutral: 'border-rt-border bg-rt-neutral-bg/40 text-rt-neutral-mid',
}

const softBadge: Record<Tone, string> = {
  success: 'bg-rt-green/15 text-rt-green',
  warning: 'bg-rt-accent-orange/15 text-rt-accent-orange',
  danger: 'bg-rt-danger/15 text-rt-danger',
  info: 'bg-rt-secondary/15 text-rt-secondary',
  neutral: 'bg-rt-border text-rt-neutral-mid',
}

/** Bordered soft panel (alerts, risk cards, connection status). */
export function toneSurface(tone: Tone): string {
  return surface[tone]
}

/** Pill / badge chip. */
export function toneBadge(tone: Tone): string {
  return softBadge[tone]
}

export type RiskLevel = 'normal' | 'warn' | 'critical'

export function riskTone(level: RiskLevel): Tone {
  if (level === 'critical') return 'danger'
  if (level === 'warn') return 'warning'
  return 'success'
}
