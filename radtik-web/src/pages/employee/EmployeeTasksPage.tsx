import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

interface Task {
  id: string
  title: string
  type: string
  status: string
}

export function EmployeeTasksPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<Task[]>('employeeTasks')
  const rows = data ?? []

  const columns: Column<Task>[] = [
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

  return (
    <div className="space-y-4">
      <Card title={t('nav.tasks')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !rows.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <DataTable columns={columns} rows={rows} />
      </Card>
    </div>
  )
}
