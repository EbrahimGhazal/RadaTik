import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'
import { StatusBadge } from '../../components/ui/StatusBadge'
import { cn } from '../../lib/cn'
import { useMockDbQuery } from '../../hooks/useMockDbQuery'

interface Plan {
  name: string
  downloadMbps: number
  uploadMbps: number
  priceMonthly: number
}

interface Inv {
  id: string
  number: string
  amount: number
  date: string
  status: string
}

export function ClientDashboard() {
  const { t } = useTranslation()
  const [submitMessage, setSubmitMessage] = useState<string | null>(null)
  const { data, loading, error } = useMockDbQuery()
  const plan = (data?.clientPlan as Plan) ?? null
  const invoices = (data?.clientInvoices as Inv[]) ?? []
  const { register, handleSubmit, reset } = useForm<{ subject: string; message: string }>()

  const onTicket = () => {
    reset()
    setSubmitMessage('Ticket submitted (mock).')
  }

  const cols: Column<Inv>[] = [
    { key: 'n', header: t('table.invoice'), cell: (r) => r.number },
    { key: 'a', header: t('table.amount'), cell: (r) => `$${r.amount}` },
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
      <MockDbQueryStatus loading={loading} error={error} showLoading={false} />
      <div className="grid gap-4 lg:grid-cols-3">
        <Card title={t('client.plan')} className="lg:col-span-2">
          {plan ? (
            <div className="grid gap-4 sm:grid-cols-2">
              <div>
                <p className="text-2xl font-semibold text-rt-neutral-text">{plan.name}</p>
                <p className="mt-2 text-sm text-rt-neutral-mid">
                  ${plan.priceMonthly} / mo · mock contracted rate
                </p>
              </div>
              <div className="rounded-lg border border-dashed border-rt-border p-4">
                <p className="text-xs uppercase text-rt-neutral-mid">{t('client.speedDown')}</p>
                <p className="text-xl font-semibold text-rt-primary">{plan.downloadMbps} Mbps</p>
                <p className="mt-3 text-xs uppercase text-rt-neutral-mid">{t('client.speedUp')}</p>
                <p className="text-xl font-semibold text-rt-secondary">{plan.uploadMbps} Mbps</p>
              </div>
            </div>
          ) : (
            <p className="text-sm text-rt-neutral-mid">{loading ? t('common.loading') : t('common.noData')}</p>
          )}
        </Card>
        <Card title={t('client.status')}>
          <ul className="space-y-2 text-sm">
            <li className="flex justify-between">
              <span className="text-rt-neutral-mid">Link</span>
              <span className="font-medium text-emerald-600 dark:text-emerald-400">Active</span>
            </li>
            <li className="flex justify-between">
              <span className="text-rt-neutral-mid">Latency</span>
              <span className="font-medium tabular-nums">14 ms</span>
            </li>
            <li className="flex justify-between">
              <span className="text-rt-neutral-mid">Speed test</span>
              <span className="font-medium tabular-nums">482↓ / 198↑</span>
            </li>
          </ul>
        </Card>
      </div>

      <Card title={t('client.usage')}>
        <div className="grid gap-4 sm:grid-cols-2">
          {[
            { label: t('client.speedDown'), value: '312 GB', hint: 'last 30 days' },
            { label: t('client.speedUp'), value: '48 GB', hint: 'last 30 days' },
          ].map((u) => (
            <div key={u.label} className="rounded-lg bg-rt-page p-4">
              <p className="text-xs uppercase tracking-wide text-rt-neutral-mid">{u.label}</p>
              <p className="mt-2 text-2xl font-semibold text-rt-neutral-text">{u.value}</p>
              <p className="text-xs text-rt-neutral-mid">{u.hint}</p>
            </div>
          ))}
        </div>
      </Card>

      <Card title={t('client.invoices')}>
        {loading && !error ? (
          <p className="mb-3 text-sm text-rt-neutral-mid">{t('common.loading')}</p>
        ) : null}
        <DataTable columns={cols} rows={invoices} />
      </Card>

      <Card title={t('client.ticket.title')}>
        {submitMessage ? <p className="mb-3 text-sm text-emerald-600">{submitMessage}</p> : null}
        <form className="space-y-3" onSubmit={handleSubmit(onTicket)}>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('client.ticket.subject')}</label>
            <input
              className={cn(
                'w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2',
              )}
              {...register('subject', { required: true })}
            />
          </div>
          <div>
            <label className="mb-1 block text-sm font-medium">{t('client.ticket.message')}</label>
            <textarea
              className="min-h-[100px] w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2"
              {...register('message', { required: true })}
            />
          </div>
          <button
            type="submit"
            className="rounded-lg bg-rt-primary px-4 py-2 text-sm font-semibold text-white shadow"
          >
            {t('client.ticket.send')}
          </button>
        </form>
      </Card>
    </div>
  )
}
