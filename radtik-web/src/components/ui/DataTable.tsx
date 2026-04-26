import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

export interface Column<T> {
  key: string
  header: ReactNode
  className?: string
  cell: (row: T) => ReactNode
}

export function DataTable<T extends { id: string }>({
  columns,
  rows,
  empty,
}: {
  columns: Column<T>[]
  rows: T[]
  empty?: ReactNode
}) {
  if (!rows.length && empty) {
    return <div className="text-sm text-rt-neutral-mid">{empty}</div>
  }

  return (
    <div className="overflow-x-auto rounded-lg border border-rt-border">
      <table className="min-w-full border-collapse text-start text-sm">
        <thead className="bg-rt-neutral-bg/80">
          <tr>
            {columns.map((c) => (
              <th
                key={c.key}
                className={cn(
                  'px-4 py-3 text-start text-xs font-medium uppercase tracking-wide text-rt-neutral-mid',
                  c.className,
                )}
              >
                {c.header}
              </th>
            ))}
          </tr>
        </thead>
        <tbody className="divide-y divide-rt-border bg-rt-surface">
          {rows.map((row, i) => (
            <tr
              key={row.id}
              className={cn(
                'transition-colors hover:bg-rt-neutral-bg/50',
                i % 2 === 1 && 'bg-rt-neutral-bg/30 dark:bg-slate-900/20',
              )}
            >
              {columns.map((c) => (
                <td key={c.key} className={cn('whitespace-nowrap px-4 py-3', c.className)}>
                  {c.cell(row)}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
