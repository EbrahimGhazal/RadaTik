import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { Card } from '../../components/ui/Card'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { ManagerMikrotikTrafficPanel } from '../../components/manager/ManagerMikrotikTrafficPanel'
import { cn } from '../../lib/cn'
import { useMockDbQuery } from '../../hooks/useMockDbQuery'

interface TeamMember {
  id: string
  name: string
  role: string
}

interface RevRow {
  p: string
  rev: number
  exp: number
}

export function ManagerDashboard() {
  const { t } = useTranslation()
  const [dashTab, setDashTab] = useState<'overview' | 'traffic'>('overview')
  const { data, loading, error } = useMockDbQuery()
  const series = (data?.revenueSeries as RevRow[]) ?? []
  const team = (data?.managerTeam as TeamMember[]) ?? []

  const usage = [
    { day: 'Mon', gbps: 3.2 },
    { day: 'Tue', gbps: 3.8 },
    { day: 'Wed', gbps: 3.5 },
    { day: 'Thu', gbps: 4.1 },
    { day: 'Fri', gbps: 4.4 },
  ]

  const points = [
    { name: 'North hub', status: 'online', collections: 128 },
    { name: 'Mall desk', status: 'sync', collections: 96 },
    { name: 'Airport kiosk', status: 'delayed', collections: 42 },
  ]

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center gap-2 border-b border-rt-border pb-3">
        <button
          type="button"
          onClick={() => setDashTab('overview')}
          className={cn(
            'rounded-lg px-3 py-2 text-sm font-medium transition-colors',
            dashTab === 'overview'
              ? 'bg-rt-primary text-white'
              : 'text-rt-neutral-mid hover:bg-rt-page',
          )}
        >
          {t('manager.dashboardTabOverview')}
        </button>
        <button
          type="button"
          onClick={() => setDashTab('traffic')}
          className={cn(
            'rounded-lg px-3 py-2 text-sm font-medium transition-colors',
            dashTab === 'traffic'
              ? 'bg-rt-primary text-white'
              : 'text-rt-neutral-mid hover:bg-rt-page',
          )}
        >
          {t('manager.dashboardTabTraffic')}
        </button>
        <Link
          to="/manager/mikrotik-traffic"
          className="ms-auto rounded-lg border border-rt-border bg-rt-surface px-3 py-2 text-sm font-medium text-rt-primary hover:bg-rt-page"
        >
          {t('manager.openTrafficFullPage')}
        </Link>
      </div>

      {dashTab === 'traffic' ? (
        <Card title={t('manager.traffic.pageTitle')} flush>
          <div className="p-4">
            <ManagerMikrotikTrafficPanel compact />
          </div>
        </Card>
      ) : null}

      {dashTab === 'overview' ? (
        <>
      <MockDbQueryStatus loading={loading} error={error} />
      <Card title={t('manager.finance')}>
        <div className="h-72">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={series}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
              <XAxis dataKey="p" tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <YAxis tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
              />
              <Area
                type="monotone"
                dataKey="rev"
                stackId="1"
                stroke="#2563eb"
                fill="#2563eb33"
              />
              <Area type="monotone" dataKey="exp" stackId="2" stroke="#f97316" fill="#f9731633" />
            </AreaChart>
          </ResponsiveContainer>
        </div>
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t('manager.team')}>
          <ul className="divide-y divide-rt-border rounded-lg border border-rt-border">
            {team.map((m) => (
              <li key={m.id} className="flex items-center justify-between px-4 py-3 text-sm">
                <div>
                  <p className="font-medium text-rt-neutral-text">{m.name}</p>
                  <p className="text-rt-neutral-mid">{m.role}</p>
                </div>
                <span className="rounded-full bg-rt-primary/10 px-2 py-1 text-xs font-medium text-rt-primary">
                  Active
                </span>
              </li>
            ))}
          </ul>
        </Card>
        <Card title={t('manager.collection')}>
          <ul className="space-y-2">
            {points.map((p) => (
              <li
                key={p.name}
                className="flex items-center justify-between rounded-lg border border-rt-border px-3 py-2 text-sm"
              >
                <div>
                  <p className="font-medium">{p.name}</p>
                  <p className="text-xs text-rt-neutral-mid">{p.status}</p>
                </div>
                <span className="tabular-nums text-rt-neutral-text">{p.collections}</span>
              </li>
            ))}
          </ul>
        </Card>
      </div>

      <Card title={t('manager.usage')}>
        <div className="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={usage}>
              <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
              <XAxis dataKey="day" tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <YAxis tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
              <Tooltip
                contentStyle={{
                  background: 'var(--color-rt-surface)',
                  border: '1px solid var(--color-rt-border)',
                }}
              />
              <Bar dataKey="gbps" fill="#06b6d4" radius={[6, 6, 0, 0]} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </Card>
        </>
      ) : null}
    </div>
  )
}
