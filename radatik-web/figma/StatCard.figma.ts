/**
 * Figma Code Connect — RadaTik StatCard
 *
 * KPI metric tile used on dashboards. Pair with Auto Layout frame 16px radius,
 * icon slot 44×44, label uppercase 12px, value 30px bold.
 */
import figma from '@figma/code-connect/react'
import { Activity } from 'lucide-react'
import { StatCard } from '../src/components/ui/StatCard'

figma.connect(
  StatCard,
  'https://www.figma.com/design/FIGMA_FILE_KEY/RadaTik-Design-System?node-id=STAT_CARD_NODE',
  {
    props: {
      label: figma.string('Label'),
      value: figma.string('Value'),
    },
    example: ({ label, value }) => <StatCard label={label} value={value} icon={Activity} />,
  },
)
