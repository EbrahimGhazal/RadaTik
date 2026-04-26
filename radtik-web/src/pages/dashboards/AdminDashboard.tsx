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
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { CoverageMap, type AccessPoint } from '../../components/maps/CoverageMap'
import { useAuthStore } from '../../store/authStore'
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
    <div className="space-y-6">
      <MockDbQueryStatus loading={loading} error={error} />
      <Card>
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <div>
            <h1 className="text-xl font-semibold text-rt-neutral-text">
              {t('admin.welcome')}, {name}
            </h1>
            <p className="mt-1 text-sm text-rt-neutral-mid">
              Cloud-scale telemetry snapshot — mock data refreshes with a gentle live jitter.
            </p>
          </div>
        </div>
      </Card>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {[
          { label: t('admin.stats.users'), value: liveUsers },
          { label: t('admin.stats.networks'), value: liveNets },
          { label: t('admin.stats.pending'), value: livePending },
        ].map((s) => (
          <Card key={s.label} flush>
            <div className="p-5">
              <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">
                {s.label}
              </p>
              <p className="mt-2 text-3xl font-semibold tabular-nums text-rt-primary">
                {s.value.toLocaleString()}
              </p>
            </div>
          </Card>
        ))}
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t('admin.traffic')}>
          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={traffic}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
                <XAxis dataKey="t" tick={{ fill: 'var(--color-rt-neutral-mid)', fontSize: 12 }} />
                <YAxis tick={{ fill: 'var(--color-rt-neutral-mid)', fontSize: 12 }} />
                <Tooltip
                  contentStyle={{
                    background: 'var(--color-rt-surface)',
                    border: '1px solid var(--color-rt-border)',
                  }}
                />
                <Line type="monotone" dataKey="gb" stroke="#2563eb" strokeWidth={2} dot={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card title={t('admin.userGrowth')}>
          <div className="h-64 w-full">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={growth}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
                <XAxis dataKey="m" tick={{ fill: 'var(--color-rt-neutral-mid)', fontSize: 12 }} />
                <YAxis tick={{ fill: 'var(--color-rt-neutral-mid)', fontSize: 12 }} />
                <Tooltip
                  contentStyle={{
                    background: 'var(--color-rt-surface)',
                    border: '1px solid var(--color-rt-border)',
                  }}
                />
                <Bar dataKey="users" fill="#06b6d4" radius={[6, 6, 0, 0]} />
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
          <ul className="space-y-3">
            {activity.map((a) => (
              <li
                key={a.id}
                className="rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm text-rt-neutral-text"
              >
                {a.title}
                <div className="mt-1 text-xs text-rt-neutral-mid">
                  {new Date(a.at).toLocaleString()}
                </div>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <Card title={t('admin.financial')}>
        <DataTable columns={financialColumns} rows={rows} />
      </Card>
    </div>
  )
}
