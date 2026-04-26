import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'
import { cn } from '../../lib/cn'

interface Interaction {
  id: string
  customer: string
  channel: string
  summary: string
  at: string
}

export function EmployeeSupportPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceQuery<Interaction[]>('supportInteractions')
  const rows = data ?? []
  const { register, handleSubmit, reset } = useForm<{ note: string }>()

  const columns: Column<Interaction>[] = [
    { key: 'c', header: t('table.customer'), cell: (r) => r.customer },
    { key: 'ch', header: t('table.channel'), cell: (r) => r.channel },
    { key: 's', header: t('nav.customerSupport'), cell: (r) => r.summary },
    { key: 'at', header: t('table.date'), cell: (r) => new Date(r.at).toLocaleString() },
  ]

  const onSave = () => {
    reset()
    alert(t('settingsPage.saved'))
  }

  return (
    <div className="space-y-4">
      <Card title={t('employeeSupport.queue')}>
        {error ? <p className="mb-4 text-sm text-red-600">{error}</p> : null}
        {loading && !rows.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <DataTable columns={columns} rows={rows} />
      </Card>
      <Card title={t('employeeSupport.notes')}>
        <form className="space-y-3" onSubmit={handleSubmit(onSave)}>
          <textarea
            className={cn(
              'min-h-[88px] w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2',
            )}
            {...register('note', { required: true })}
          />
          <button
            type="submit"
            className="rounded-lg bg-rt-primary px-4 py-2 text-sm font-semibold text-white shadow"
          >
            {t('employeeSupport.save')}
          </button>
        </form>
      </Card>
    </div>
  )
}
