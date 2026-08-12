import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

/** Standard page vertical rhythm — same spacing on every role/page. */
export function PageContent({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('space-y-6', className)}>{children}</div>
}
