import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'
import { cn } from '../../lib/cn'

interface FinRow {
  id: string
  customer: string
  amount: number
  date: string
  status: string
}

export function AdminPaymentsPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<FinRow[]>('financialRows')
  const [status, setStatus] = useState<string>('')

  const filtered = useMemo(() => {
    const rows = data ?? []
    if (!status) return rows
    return rows.filter((r) => r.status === status)
  }, [data, status])

  const columns: Column<FinRow>[] = [
    { key: 'c', header: t('table.customer'), cell: (r) => r.customer },
    { key: 'a', header: t('table.amount'), cell: (r) => `$${r.amount.toLocaleString()}` },
    { key: 'd', header: t('table.date'), cell: (r) => r.date },
    {
      key: 's',
      header: t('table.status'),
      cell: (r) => (
        <StatusBadge
          label={r.status === 'paid' ? t('status.paid') : t('status.pending')}
          code={r.status}
        />
      ),
    },
  ]

  return (
    <div className="space-y-4">
      <Card title={t('pages.payments')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !data?.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <div className="mb-4 flex flex-wrap items-end gap-3">
          <div>
            <label className="mb-1 block text-xs font-medium text-rt-neutral-mid">
              {t('paymentsPage.filterStatus')}
            </label>
            <select
              value={status}
              onChange={(e) => setStatus(e.target.value)}
              className={cn(
                'rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2',
              )}
            >
              <option value="">{t('paymentsPage.all')}</option>
              <option value="paid">{t('status.paid')}</option>
              <option value="pending">{t('status.pending')}</option>
            </select>
          </div>
        </div>
        <DataTable columns={columns} rows={filtered} />
      </Card>
    </div>
  )
}
