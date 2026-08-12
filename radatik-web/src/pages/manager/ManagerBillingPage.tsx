import { useTranslation } from 'react-i18next'
import { Area, AreaChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { fetchResource } from '../../lib/api'
import { chartAxisTick, chartColors, chartGrid, chartTooltipStyle } from '../../lib/chartTheme'
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
    <PageContent>
      <Card title={t('nav.billing')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={series.length > 0 || rows.length > 0} />
        </div>
        <div className="mb-8 h-56 sm:h-64">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={series}>
              <CartesianGrid {...chartGrid} />
              <XAxis dataKey="p" tick={chartAxisTick} />
              <YAxis tick={chartAxisTick} />
              <Tooltip contentStyle={chartTooltipStyle} />
              <Area type="monotone" dataKey="rev" stroke={chartColors.primary} fill={chartColors.primaryFill} />
              <Area type="monotone" dataKey="exp" stroke={chartColors.orange} fill={chartColors.orangeFill} />
            </AreaChart>
          </ResponsiveContainer>
        </div>
        <h3 className="mb-2 text-sm font-semibold text-rt-neutral-text">{t('pages.payments')}</h3>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </PageContent>
  )
}
