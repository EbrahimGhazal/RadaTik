/**
 * Figma Code Connect — RadaTik Button
 *
 * Link this file to your Figma design system component once the Figma MCP
 * file URL is available. Replace FIGMA_FILE_KEY and node id below.
 *
 * Design tokens: --color-rt-primary, --color-rt-surface, --color-rt-border
 * Variants: primary | secondary | ghost | danger
 * Sizes: sm | md | lg
 */
import figma from '@figma/code-connect/react'
import { Button } from '../src/components/ui/Button'

figma.connect(
  Button,
  'https://www.figma.com/design/FIGMA_FILE_KEY/RadaTik-Design-System?node-id=BUTTON_NODE',
  {
    props: {
      variant: figma.enum('Variant', {
        Primary: 'primary',
        Secondary: 'secondary',
        Ghost: 'ghost',
        Danger: 'danger',
      }),
      size: figma.enum('Size', {
        Small: 'sm',
        Medium: 'md',
        Large: 'lg',
      }),
      label: figma.string('Label'),
      disabled: figma.boolean('Disabled'),
    },
    example: ({ variant, size, label, disabled }) => (
      <Button variant={variant} size={size} disabled={disabled}>
        {label}
      </Button>
    ),
  },
)
