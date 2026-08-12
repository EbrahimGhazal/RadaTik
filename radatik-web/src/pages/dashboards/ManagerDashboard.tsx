import { useState } from 'react'
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
import { LinkButton } from '../../components/ui/LinkButton'
import { ListRow, ListStack } from '../../components/ui/ListRow'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { PageContent } from '../../components/ui/PageContent'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { TabGroup } from '../../components/ui/TabGroup'
import { ManagerMikrotikTrafficPanel } from '../../components/manager/ManagerMikrotikTrafficPanel'
import { chartAxisTick, chartColors, chartGrid, chartTooltipStyle } from '../../lib/chartTheme'
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
    <PageContent>
      <div className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center sm:justify-between">
        <TabGroup
          value={dashTab}
          onChange={setDashTab}
          tabs={[
            { id: 'overview', label: t('manager.dashboardTabOverview') },
            { id: 'traffic', label: t('manager.dashboardTabTraffic') },
          ]}
        />
        <LinkButton to="/manager/mikrotik-traffic">{t('manager.openTrafficFullPage')}</LinkButton>
      </div>

      {dashTab === 'traffic' ? (
        <Card title={t('manager.traffic.pageTitle')} flush>
          <div className="p-4 sm:p-5">
            <ManagerMikrotikTrafficPanel compact />
          </div>
        </Card>
      ) : null}

      {dashTab === 'overview' ? (
        <>
          <MockDbQueryStatus loading={loading} error={error} />
          <Card title={t('manager.finance')}>
            <div className="h-64 w-full sm:h-72">
              <ResponsiveContainer width="100%" height="100%">
                <AreaChart data={series}>
                  <CartesianGrid {...chartGrid} />
                  <XAxis dataKey="p" tick={chartAxisTick} />
                  <YAxis tick={chartAxisTick} />
                  <Tooltip contentStyle={chartTooltipStyle} />
                  <Area
                    type="monotone"
                    dataKey="rev"
                    stackId="1"
                    stroke={chartColors.primary}
                    fill={chartColors.primaryFill}
                  />
                  <Area
                    type="monotone"
                    dataKey="exp"
                    stackId="2"
                    stroke={chartColors.orange}
                    fill={chartColors.orangeFill}
                  />
                </AreaChart>
              </ResponsiveContainer>
            </div>
          </Card>

          <div className="grid gap-4 md:grid-cols-2">
            <Card title={t('manager.team')}>
              <ul className="rt-divide-list">
                {team.map((m) => (
                  <li key={m.id} className="flex items-center justify-between gap-3 px-4 py-3.5 text-sm">
                    <div className="min-w-0">
                      <p className="font-medium text-rt-neutral-text">{m.name}</p>
                      <p className="text-xs text-rt-neutral-mid">{m.role}</p>
                    </div>
                    <StatusBadge label={t('status.active')} code="active" />
                  </li>
                ))}
              </ul>
            </Card>
            <Card title={t('manager.collection')}>
              <ListStack>
                {points.map((p) => (
                  <ListRow
                    key={p.name}
                    title={p.name}
                    subtitle={p.status}
                    trailing={
                      <span className="tabular-nums font-medium text-rt-neutral-text">{p.collections}</span>
                    }
                  />
                ))}
              </ListStack>
            </Card>
          </div>

          <Card title={t('manager.usage')}>
            <div className="h-64 w-full">
              <ResponsiveContainer width="100%" height="100%">
                <BarChart data={usage}>
                  <CartesianGrid {...chartGrid} />
                  <XAxis dataKey="day" tick={chartAxisTick} />
                  <YAxis tick={chartAxisTick} />
                  <Tooltip contentStyle={chartTooltipStyle} />
                  <Bar dataKey="gbps" fill={chartColors.secondary} radius={[8, 8, 0, 0]} />
                </BarChart>
              </ResponsiveContainer>
            </div>
          </Card>
        </>
      ) : null}
    </PageContent>
  )
}
