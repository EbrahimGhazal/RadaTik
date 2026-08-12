import { useTranslation } from 'react-i18next'
import {
  Bar,
  BarChart,
  CartesianGrid,
  Line,
  LineChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import type { FeatureCollection } from 'geojson'
import { Activity, CreditCard, Users } from 'lucide-react'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { ListRow, ListStack } from '../../components/ui/ListRow'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { PageContent } from '../../components/ui/PageContent'
import { StatCard } from '../../components/ui/StatCard'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { CoverageMap, type AccessPoint } from '../../components/maps/CoverageMap'
import { useAuthStore } from '../../store/authStore'
import { chartAxisTick, chartColors, chartGrid, chartTooltipStyle } from '../../lib/chartTheme'
import { useLiveStat } from '../../hooks/useLiveStat'
import { useMockDbQuery } from '../../hooks/useMockDbQuery'

interface FinancialRow {
  id: string
  customer: string
  amount: number
  date: string
  status: string
}

export function AdminDashboard() {
  const { t } = useTranslation()
  const name = useAuthStore((s) => s.user?.fullName ?? '')
  const { data, loading, error } = useMockDbQuery()
  const traffic = (data?.trafficSeries as Array<{ t: string; gb: number }>) ?? []
  const growth = (data?.userGrowth as Array<{ m: string; users: number }>) ?? []
  const points = (data?.accessPoints as AccessPoint[]) ?? []
  const zones = (data?.coverageZones as FeatureCollection) ?? null
  const activity = (data?.activity as Array<{ id: string; title: string; at: string }>) ?? []
  const rows = (data?.financialRows as FinancialRow[]) ?? []
  const baseStats =
    ((data?.stats as { admin?: { totalUsers: number; activeNetworks: number; pendingPayments: number } })?.admin as
      | { totalUsers: number; activeNetworks: number; pendingPayments: number }
      | undefined) ?? { totalUsers: 0, activeNetworks: 0, pendingPayments: 0 }

  const liveUsers = useLiveStat(baseStats.totalUsers)
  const liveNets = useLiveStat(baseStats.activeNetworks)
  const livePending = useLiveStat(baseStats.pendingPayments)

  const financialColumns: Column<FinancialRow>[] = [
    { key: 'c', header: t('table.customer'), cell: (r) => r.customer },
    {
      key: 'a',
      header: t('table.amount'),
      cell: (r) => `$${r.amount.toLocaleString()}`,
    },
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

      <Card flush className="overflow-hidden">
        <div className="relative bg-gradient-to-r from-rt-primary/10 via-rt-secondary/5 to-transparent p-5 sm:p-6">
          <p className="text-sm text-rt-neutral-mid">{t('admin.welcome')}</p>
          <p className="mt-1 text-lg font-semibold text-rt-neutral-text sm:text-xl">{name}</p>
          <p className="mt-2 max-w-2xl text-sm text-rt-neutral-mid">{t('admin.dashboardHint')}</p>
        </div>
      </Card>

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        <StatCard label={t('admin.stats.users')} value={liveUsers} icon={Users} />
        <StatCard label={t('admin.stats.networks')} value={liveNets} icon={Activity} />
        <StatCard label={t('admin.stats.pending')} value={livePending} icon={CreditCard} />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t('admin.traffic')}>
          <div className="h-64 w-full sm:h-72">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={traffic}>
                <CartesianGrid {...chartGrid} />
                <XAxis dataKey="t" tick={chartAxisTick} />
                <YAxis tick={chartAxisTick} />
                <Tooltip contentStyle={chartTooltipStyle} />
                <Line type="monotone" dataKey="gb" stroke={chartColors.primary} strokeWidth={2.5} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card title={t('admin.userGrowth')}>
          <div className="h-64 w-full sm:h-72">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={growth}>
                <CartesianGrid {...chartGrid} />
                <XAxis dataKey="m" tick={chartAxisTick} />
                <YAxis tick={chartAxisTick} />
                <Tooltip contentStyle={chartTooltipStyle} />
                <Bar dataKey="users" fill={chartColors.secondary} radius={[8, 8, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>
      </div>

      <div className="grid gap-4 xl:grid-cols-3">
        <Card title={t('admin.coverage')} className="xl:col-span-2">
          {!error && (zones && points.length ? (
            <CoverageMap zones={zones} points={points} />
          ) : loading ? null : (
            <p className="text-sm text-rt-neutral-mid">{t('common.noData')}</p>
          ))}
        </Card>
        <Card title={t('admin.activity')}>
          <ListStack>
            {activity.map((a) => (
              <ListRow
                key={a.id}
                title={a.title}
                subtitle={new Date(a.at).toLocaleString()}
              />
            ))}
          </ListStack>
        </Card>
      </div>

      <Card title={t('admin.financial')}>
        <DataTable columns={financialColumns} rows={rows} />
      </Card>
    </PageContent>
  )
}
