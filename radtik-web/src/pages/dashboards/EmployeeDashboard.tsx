import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'
import { Bar, BarChart, CartesianGrid, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockDbQuery } from '../../hooks/useMockDbQuery'

interface Task {
  id: string
  title: string
  type: string
  status: string
}

interface Interaction {
  id: string
  customer: string
  channel: string
  summary: string
  at: string
}

export function EmployeeDashboard() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockDbQuery()
  const tasks = (data?.employeeTasks as Task[]) ?? []
  const interactions = (data?.supportInteractions as Interaction[]) ?? []
  const resp = useMemo(
    () => [
      { day: 'Mon', min: 12 },
      { day: 'Tue', min: 9 },
      { day: 'Wed', min: 15 },
      { day: 'Thu', min: 8 },
      { day: 'Fri', min: 10 },
    ],
    [],
  )
  const completion = useMemo(
    () => [
      { week: 'W1', pct: 72 },
      { week: 'W2', pct: 78 },
      { week: 'W3', pct: 84 },
      { week: 'W4', pct: 88 },
    ],
    [],
  )

  const done = tasks.filter((x) => x.status === 'done').length
  const pending = tasks.filter((x) => x.status !== 'done').length

  const taskColumns: Column<Task>[] = [
    { key: 'title', header: t('nav.tasks'), cell: (r) => r.title },
    { key: 'type', header: t('table.type'), cell: (r) => r.type },
    {
      key: 'status',
      header: t('table.status'),
      cell: (r) => (
        <StatusBadge
          label={
            r.status === 'done'
              ? t('status.done')
              : r.status === 'pending'
                ? t('status.pending')
                : t('status.inProgress')
          }
          code={r.status === 'in_progress' ? 'in_progress' : r.status}
        />
      ),
    },
  ]

  const interColumns: Column<Interaction>[] = [
    { key: 'c', header: t('table.customer'), cell: (r) => r.customer },
    { key: 'ch', header: t('table.channel'), cell: (r) => r.channel },
    {
      key: 's',
      header: t('nav.customerSupport'),
      cell: (r) => <span className="max-w-[240px] truncate">{r.summary}</span>,
    },
  ]

  return (
    <div className="space-y-6">
      <MockDbQueryStatus loading={loading} error={error} />
      <div className="grid gap-4 md:grid-cols-2">
        <Card title={t('employee.stats.done')}>
          <p className="text-4xl font-semibold text-rt-primary">{done}</p>
        </Card>
        <Card title={t('employee.stats.pending')}>
          <p className="text-4xl font-semibold text-rt-accent-orange">{pending}</p>
        </Card>
      </div>

      <Card title={t('employee.tasks')}>
        <DataTable columns={taskColumns} rows={tasks} />
      </Card>

      <div className="grid gap-4 lg:grid-cols-2">
        <Card title={t('employee.responseTime')}>
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={resp}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
                <XAxis dataKey="day" tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
                <YAxis tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
                <Tooltip
                  contentStyle={{
                    background: 'var(--color-rt-surface)',
                    border: '1px solid var(--color-rt-border)',
                  }}
                />
                <Line type="monotone" dataKey="min" stroke="#7c3aed" strokeWidth={2} dot />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </Card>
        <Card title={t('employee.completion')}>
          <div className="h-56">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={completion}>
                <CartesianGrid strokeDasharray="3 3" stroke="var(--color-rt-border)" />
                <XAxis dataKey="week" tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} />
                <YAxis tick={{ fontSize: 12, fill: 'var(--color-rt-neutral-mid)' }} domain={[0, 100]} />
                <Tooltip
                  contentStyle={{
                    background: 'var(--color-rt-surface)',
                    border: '1px solid var(--color-rt-border)',
                  }}
                />
                <Bar dataKey="pct" fill="#10b981" radius={[6, 6, 0, 0]} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </Card>
      </div>

      <Card title={t('employee.interactions')}>
        <DataTable columns={interColumns} rows={interactions} />
      </Card>
    </div>
  )
}
