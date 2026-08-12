/**
 * Figma Code Connect — RadaTik Card
 *
 * Maps the dashboard Card shell to Figma. Tokens: bg-rt-surface, border-rt-border,
 * shadow-rt-sm, rounded-2xl (16px).
 */
import figma from '@figma/code-connect/react'
import { Card } from '../src/components/ui/Card'

figma.connect(
  Card,
  'https://www.figma.com/design/FIGMA_FILE_KEY/RadaTik-Design-System?node-id=CARD_NODE',
  {
    props: {
      title: figma.string('Title'),
      elevated: figma.boolean('Elevated'),
      flush: figma.boolean('Flush'),
      body: figma.string('Body'),
    },
    example: ({ title, elevated, flush, body }) => (
      <Card title={title} elevated={elevated} flush={flush}>
        {body}
      </Card>
    ),
  },
)
