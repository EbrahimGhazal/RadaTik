import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

interface PayRow {
  id: string
  customer: string
  amount: number
  date: string
  status: string
}

export function CollectionTransactionsPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<PayRow[]>('collectionPayments')
  const rows = data ?? []

  const columns: Column<PayRow>[] = [
    { key: 'c', header: t('table.customer'), cell: (r) => r.customer },
    { key: 'a', header: t('table.amount'), cell: (r) => `$${r.amount}` },
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
      <Card title={t('transactionsPage.title')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={rows.length > 0} />
        </div>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </PageContent>
  )
}
