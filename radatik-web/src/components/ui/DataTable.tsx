import type { ReactNode } from 'react'
import { cn } from '../../lib/cn'

export interface Column<T> {
  key: string
  header: ReactNode
  className?: string
  /** Used for mobile card layout label */
  mobileLabel?: string
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
    return (
      <div className="rounded-xl border border-dashed border-rt-border bg-rt-neutral-bg/30 px-4 py-10 text-center text-sm text-rt-neutral-mid">
        {empty}
      </div>
    )
  }

  return (
    <>
      {/* Mobile / small tablet — card stack */}
      <div className="space-y-3 md:hidden">
        {rows.map((row) => (
          <article
            key={row.id}
            className="rounded-xl border border-rt-border bg-rt-neutral-bg/20 p-4 shadow-[var(--shadow-rt-sm)] transition-colors hover:border-rt-primary/20"
          >
            <dl className="space-y-2.5">
              {columns.map((c) => (
                <div key={c.key} className="flex items-start justify-between gap-3 text-sm">
                  <dt className="shrink-0 text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">
                    {c.mobileLabel ?? c.header}
                  </dt>
                  <dd className={cn('min-w-0 text-end text-rt-neutral-text', c.className)}>{c.cell(row)}</dd>
                </div>
              ))}
            </dl>
          </article>
        ))}
      </div>

      {/* Tablet+ — table */}
      <div className="hidden overflow-x-auto rounded-xl border border-rt-border shadow-[var(--shadow-rt-sm)] md:block">
        <table className="min-w-full border-collapse text-start text-sm">
          <thead>
            <tr className="border-b border-rt-border bg-rt-neutral-bg/60">
              {columns.map((c) => (
                <th
                  key={c.key}
                  className={cn(
                    'px-4 py-3.5 text-start text-[11px] font-semibold uppercase tracking-wider text-rt-neutral-mid lg:px-5',
                    c.className,
                  )}
                >
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody className="divide-y divide-rt-border bg-rt-surface">
            {rows.map((row) => (
              <tr key={row.id} className="transition-colors hover:bg-rt-primary-soft/30">
                {columns.map((c) => (
                  <td
                    key={c.key}
                    className={cn(
                      'whitespace-nowrap px-4 py-3.5 text-rt-neutral-text lg:px-5',
                      c.className,
                    )}
                  >
                    {c.cell(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  )
}
