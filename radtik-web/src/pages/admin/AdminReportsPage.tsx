import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Cell, Pie, PieChart, ResponsiveContainer, Tooltip } from 'recharts'
import { Card } from '../../components/ui/Card'
import { fetchResource } from '../../lib/api'
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
      { name: t('status.paid'), value: paid, fill: '#10b981' },
      { name: t('status.pending'), value: pending, fill: '#f59e0b' },
    ].filter((d) => d.value > 0)
  }, [data, t])

  return (
    <div className="space-y-4">
      <Card title={t('pages.reports')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        <p className="mb-6 text-sm text-rt-neutral-mid">{t('reportsPage.kpis')}</p>
        {stats ? (
          <div className="mb-8 grid gap-4 sm:grid-cols-3">
            {[
              { label: t('admin.stats.users'), value: stats.totalUsers },
              { label: t('admin.stats.networks'), value: stats.activeNetworks },
              { label: t('admin.stats.pending'), value: stats.pendingPayments },
            ].map((k) => (
              <div
                key={k.label}
                className="rounded-xl border border-rt-border bg-rt-page px-4 py-3 text-center"
              >
                <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">
                  {k.label}
                </p>
                <p className="mt-2 text-2xl font-semibold text-rt-primary">
                  {k.value.toLocaleString()}
                </p>
              </div>
            ))}
          </div>
        ) : (
          <p className="mb-8 text-sm text-rt-neutral-mid">
            {loading ? t('common.loading') : t('common.noData')}
          </p>
        )}
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
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
              />
            </PieChart>
          </ResponsiveContainer>
        </div>
      </Card>
    </div>
  )
}
