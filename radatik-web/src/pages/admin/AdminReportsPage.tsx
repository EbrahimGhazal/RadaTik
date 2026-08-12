import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import { Card } from '../../components/ui/Card'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { StatCard } from '../../components/ui/StatCard'
import { fetchResource } from '../../lib/api'
import { chartColors, chartTooltipStyle } from '../../lib/chartTheme'
import { useMockResourceBundleQuery } from '../../hooks/useMockResourceQuery'

interface FinRow {
  id: string
  status: string
}

interface AdminStats {
  totalUsers: number
  activeNetworks: number
  pendingPayments: number
}

export function AdminReportsPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceBundleQuery(
    'stats+financialRows',
    async () => {
      const [s, f] = await Promise.all([
        fetchResource<{ admin: AdminStats }>('stats'),
        fetchResource<FinRow[]>('financialRows'),
      ])
      return { stats: s.admin, rows: f }
    },
  )
  const stats = data?.stats ?? null

  const pieData = useMemo(() => {
    const rows = data?.rows ?? []
    const paid = rows.filter((r) => r.status === 'paid').length
    const pending = rows.filter((r) => r.status === 'pending').length
    return [
      { name: t('status.paid'), value: paid, fill: chartColors.green },
      { name: t('status.pending'), value: pending, fill: chartColors.orange },
    ].filter((d) => d.value > 0)
  }, [data, t])

  return (
    <PageContent>
      <Card title={t('pages.reports')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={Boolean(stats)} />
        </div>
        <p className="mb-6 text-sm text-rt-neutral-mid">{t('reportsPage.kpis')}</p>
        {stats ? (
          <div className="mb-8 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <StatCard label={t('admin.stats.users')} value={stats.totalUsers} />
            <StatCard label={t('admin.stats.networks')} value={stats.activeNetworks} />
            <StatCard label={t('admin.stats.pending')} value={stats.pendingPayments} />
          </div>
        ) : !loading && !error ? (
          <p className="mb-8 text-sm text-rt-neutral-mid">{t('common.noData')}</p>
        ) : null}
        <h3 className="mb-2 text-sm font-semibold text-rt-neutral-text">
          {t('reportsPage.paymentMix')}
        </h3>
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie data={pieData} dataKey="value" nameKey="name" innerRadius={50} outerRadius={80} paddingAngle={2}>
                {pieData.map((entry) => (
                  <Cell key={entry.name} fill={entry.fill} />
                ))}
              </Pie>
              <Tooltip contentStyle={chartTooltipStyle} />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </Card>
    </PageContent>
  )
}
