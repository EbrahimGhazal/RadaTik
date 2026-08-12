import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
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
    <PageContent>
      <Card title={t('nav.tasks')}>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={rows.length > 0} />
        </div>
        <DataTable columns={columns} rows={rows} />
      </Card>
    </PageContent>
  )
}
