import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Alert } from '../../components/ui/Alert'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { FieldLabel, Textarea } from '../../components/ui/Input'
import { PageContent } from '../../components/ui/PageContent'
import { useMockResourceQuery } from '../../hooks/useMockResourceQuery'

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
    <PageContent>
      <Card title={t('employeeSupport.queue')}>
        {error ? <Alert variant="danger">{error}</Alert> : null}
        {loading && !rows.length ? (
          <p className="mb-4 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <DataTable columns={columns} rows={rows} />
      </Card>
      <Card title={t('employeeSupport.notes')}>
        <form className="space-y-4" onSubmit={handleSubmit(onSave)}>
          <div>
            <FieldLabel htmlFor="support-note">{t('employeeSupport.notes')}</FieldLabel>
            <Textarea id="support-note" {...register('note', { required: true })} />
          </div>
          <Button type="submit">{t('employeeSupport.save')}</Button>
        </form>
      </Card>
    </PageContent>
  )
}
