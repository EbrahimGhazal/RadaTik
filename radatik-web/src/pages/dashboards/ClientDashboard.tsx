import { useState } from 'react'

import { useForm } from 'react-hook-form'

import { useTranslation } from 'react-i18next'

import { Alert } from '../../components/ui/Alert'

import { Button } from '../../components/ui/Button'

import { Card } from '../../components/ui/Card'

import { DataTable, type Column } from '../../components/ui/DataTable'

import { FieldLabel, Input, Textarea } from '../../components/ui/Input'

import { KeyValueList } from '../../components/ui/ListRow'

import { MockDbQueryStatus } from '../../components/ui/MockDbQueryStatus'

import { PageContent } from '../../components/ui/PageContent'

import { StatusBadge } from '../../components/ui/StatusBadge'

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

    setSubmitMessage(t('client.ticket.sent'))

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

    <PageContent>

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

              <div className="rt-kpi grid grid-cols-2 gap-4">

                <div>

                  <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">

                    {t('client.speedDown')}

                  </p>

                  <p className="mt-2 text-xl font-semibold text-rt-primary">{plan.downloadMbps} Mbps</p>

                </div>

                <div>

                  <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">

                    {t('client.speedUp')}

                  </p>

                  <p className="mt-2 text-xl font-semibold text-rt-secondary">{plan.uploadMbps} Mbps</p>

                </div>

              </div>

            </div>

          ) : (

            <p className="text-sm text-rt-neutral-mid">{loading ? t('common.loading') : t('common.noData')}</p>

          )}

        </Card>

        <Card title={t('client.status')}>

          <KeyValueList

            items={[

              { label: t('client.linkStatus'), value: t('status.active'), valueClassName: 'text-rt-green' },

              { label: t('client.latency'), value: '14 ms' },

              { label: t('client.speedTest'), value: '482↓ / 198↑' },

            ]}

          />

        </Card>

      </div>



      <Card title={t('client.usage')}>

        <div className="grid gap-4 sm:grid-cols-2">

          {[

            { label: t('client.speedDown'), value: '312 GB', hint: 'last 30 days' },

            { label: t('client.speedUp'), value: '48 GB', hint: 'last 30 days' },

          ].map((u) => (

            <div key={u.label} className="rt-kpi">

              <p className="text-xs font-medium uppercase tracking-wide text-rt-neutral-mid">{u.label}</p>

              <p className="mt-2 text-2xl font-semibold text-rt-neutral-text">{u.value}</p>

              <p className="mt-1 text-xs text-rt-neutral-mid">{u.hint}</p>

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

        {submitMessage ? <Alert variant="success">{submitMessage}</Alert> : null}

        <form className="mt-4 space-y-4" onSubmit={handleSubmit(onTicket)}>

          <div>

            <FieldLabel htmlFor="client-ticket-subject">{t('client.ticket.subject')}</FieldLabel>

            <Input id="client-ticket-subject" {...register('subject', { required: true })} />

          </div>

          <div>

            <FieldLabel htmlFor="client-ticket-message">{t('client.ticket.message')}</FieldLabel>

            <Textarea id="client-ticket-message" {...register('message', { required: true })} />

          </div>

          <Button type="submit">{t('client.ticket.send')}</Button>

        </form>

      </Card>

    </PageContent>

  )

}


