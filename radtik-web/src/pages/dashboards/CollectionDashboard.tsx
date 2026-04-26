import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockDbQuery } from '../../hooks/useMockDbQuery'

interface PayRow {
  id: string
  customer: string
  amount: number
  date: string
  status: string
}

export function CollectionDashboard() {
  const { t } = useTranslation()
  const [message, setMessage] = useState<string | null>(null)
  const { data, loading, error } = useMockDbQuery()
  const rows = (data?.collectionPayments as PayRow[]) ?? []
  const chart = (data?.collectionsChart as Array<{ d: string; amt: number }>) ?? []

  const total = rows.reduce((s, r) => s + r.amount, 0)

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
    <div className="space-y-6">
      <MockDbQueryStatus loading={loading} error={error} />
      <Card title={t('collection.summary')}>
        <div className="flex flex-wrap items-end gap-6">
          <div>
            <p className="text-xs uppercase tracking-wide text-rt-neutral-mid">Today (mock)</p>
            <p className="text-4xl font-semibold text-rt-primary">${total.toLocaleString()}</p>
          </div>
          <button
            type="button"
            className="rounded-lg bg-rt-accent-orange px-4 py-2 text-sm font-semibold text-white shadow transition hover:opacity-95"
            onClick={() => setMessage('Record payment (mock flow).')}
          >
            {t('collection.record')}
          </button>
        </div>
        {message ? <p className="mt-3 text-sm text-emerald-600">{message}</p> : null}
      </Card>

      <Card title={t('collection.chart')}>
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chart}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
              <XAxis dataKey="d" tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <YAxis tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
              />
              <Bar dataKey="amt" fill="#fb923c" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </Card>

      <Card title={t('collection.table')}>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </div>
  )
}
