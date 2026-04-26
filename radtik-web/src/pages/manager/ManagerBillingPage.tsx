import { useTranslation } from 'react-i18next'
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { fetchResource } from '../../lib/api'
import { useMockResourceBundleQuery } from '../../hooks/useMockResourceQuery'

interface FinRow {
  id: string
  customer: string
  amount: number
  date: string
  status: string
}

interface RevRow {
  p: string
  rev: number
  exp: number
}

export function ManagerBillingPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceBundleQuery(
    'revenueSeries+financialRows',
    async () => {
      const [rev, fin] = await Promise.all([
        fetchResource<RevRow[]>('revenueSeries'),
        fetchResource<FinRow[]>('financialRows'),
      ])
      return { series: rev, rows: fin }
    },
  )
  const series = data?.series ?? []
  const rows = data?.rows ?? []

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
      <Card title={t('nav.billing')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !series.length && !rows.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <div className="mb-8 h-56">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={series}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
              <XAxis dataKey="p" tick={{ fontSize: 11, fill: 'var(--color-rt-neutral-mid)' }} />
              <YAxis tick={{ fontSize: 11, fill: 'var(--color-rt-neutral-mid)' }} />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
              />
              <Area type="monotone" dataKey="rev" stroke="#2563eb" fill="#2563eb22" />
              <Area type="monotone" dataKey="exp" stroke="#f97316" fill="#f9731622" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
        <h3 className="mb-2 text-sm font-semibold text-rt-neutral-text">{t('pages.payments')}</h3>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </div>
  )
}
