/**
 * RadaTik SPA design tokens — reference for Figma variables library.
 *
 * Sync these with Figma local variables (Light + Dark modes):
 *
 * | Token              | Light     | Dark      |
 * |--------------------|-----------|-----------|
 * | rt-primary         | #2563eb   | #60a5fa   |
 * | rt-secondary       | #0891b2   | #22d3ee   |
 * | rt-page            | #f1f5f9   | #050810   |
 * | rt-surface         | #ffffff   | #0c1222   |
 * | rt-elevated        | #ffffff   | #151d2e   |
 * | rt-neutral-text    | #0f172a   | #f1f5f9   |
 * | rt-neutral-mid     | #64748b   | #94a3b8   |
 * | rt-border          | #e2e8f0   | slate/12% |
 * | rt-green           | #059669   | #34d399   |
 * | rt-danger          | #dc2626   | #f87171   |
 *
 * Typography: Poppins (EN), Cairo (AR)
 * Radius: buttons xl (12px), cards 2xl (16px), inputs xl (12px)
 * Breakpoints: mobile <768, tablet md 768+, laptop lg 1024+, xl 1280+
 */
export const designTokens = {
  light: {
    primary: '#2563eb',
    page: '#f1f5f9',
    surface: '#ffffff',
  },
  dark: {
    primary: '#60a5fa',
    page: '#050810',
    surface: '#0c1222',
  },
} as const
