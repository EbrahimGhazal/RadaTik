import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { PageContent } from '../../components/ui/PageContent'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { chartAxisTick, chartColors, chartGrid, chartTooltipStyle } from '../../lib/chartTheme'
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
    <PageContent>
      <MockDbQueryStatus loading={loading} error={error} />
      <Card title={t('collection.summary')}>
        <div className="flex flex-col gap-4 sm:flex-row sm:flex-wrap sm:items-end sm:justify-between">
          <div className="rt-kpi min-w-[10rem]">
            <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">
              {t('collection.todayTotal')}
            </p>
            <p className="mt-2 text-3xl font-bold tabular-nums text-rt-primary">${total.toLocaleString()}</p>
          </div>
          <Button variant="primary" onClick={() => setMessage(t('collection.recordMock'))}>
            {t('collection.record')}
          </Button>
        </div>
        {message ? <Alert variant="success" className="mt-4">{message}</Alert> : null}
      </Card>

      <Card title={t('collection.chart')}>
        <div className="h-64 sm:h-72">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={chart}>
              <CartesianGrid {...chartGrid} />
              <XAxis dataKey="d" tick={chartAxisTick} />
              <YAxis tick={chartAxisTick} />
              <Tooltip contentStyle={chartTooltipStyle} />
              <Bar dataKey="amt" fill={chartColors.orange} radius={[8, 8, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </Card>

      <Card title={t('collection.table')}>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </PageContent>
  )
}
