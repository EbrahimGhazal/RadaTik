import { useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { FieldLabel, Select } from '../../components/ui/Input'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

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
    <PageContent>
      <Card title={t('pages.payments')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={Boolean(data?.length)} />
        </div>
        <div className="mb-4 flex flex-wrap items-end gap-3">
          <div className="sm:w-48">
            <FieldLabel>{t('paymentsPage.filterStatus')}</FieldLabel>
            <Select value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="">{t('paymentsPage.all')}</option>
              <option value="paid">{t('status.paid')}</option>
              <option value="pending">{t('status.pending')}</option>
            </Select>
          </div>
        </div>
        <DataTable columns={columns} rows={filtered} />
      </Card>
    </PageContent>
  )
}
