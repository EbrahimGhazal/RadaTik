import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

interface Inv {
  id: string
  number: string
  amount: number
  date: string
  status: string
}

export function ClientBillingPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<Inv[]>('clientInvoices')
  const rows = data ?? []

  const columns: Column<Inv>[] = [
    { key: 'n', header: t('table.invoice'), cell: (r) => r.number },
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
    <div className="space-y-4">
      <Card title={t('nav.billing')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !rows.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <DataTable columns={columns} rows={rows} />
      </Card>
    </div>
  )
}
