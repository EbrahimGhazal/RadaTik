/** Shared Recharts styling — uses CSS design tokens for light/dark consistency. */

export const chartGrid = { strokeDasharray: '3 3', stroke: 'var(--color-rt-border)' }

export const chartAxisTick = { fill: 'var(--color-rt-neutral-mid)', fontSize: 12 }

export const chartTooltipStyle = {
  background: 'var(--color-rt-elevated)',
  border: '1px solid var(--color-rt-border)',
  borderRadius: '12px',
  color: 'var(--color-rt-neutral-text)',
  boxShadow: 'var(--shadow-rt-md)',
  fontSize: '13px',
}

export const chartColors = {
  primary: 'var(--color-rt-primary)',
  secondary: 'var(--color-rt-secondary)',
  green: 'var(--color-rt-green)',
  orange: 'var(--color-rt-accent-orange)',
  purple: 'var(--color-rt-accent-purple)',
  primaryFill: 'color-mix(in srgb, var(--color-rt-primary) 20%, transparent)',
  secondaryFill: 'color-mix(in srgb, var(--color-rt-secondary) 20%, transparent)',
  orangeFill: 'color-mix(in srgb, var(--color-rt-accent-orange) 20%, transparent)',
}
