import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Card } from '../../components/ui/Card'
import { cn } from '../../lib/cn'

export function ClientSupportPage() {
  const { t } = useTranslation()
  const { register, handleSubmit, reset } = useForm<{ subject: string; message: string }>()

  const onSubmit = () => {
    reset()
    alert(t('client.ticket.title') + ' — OK (mock)')
  }

  return (
    <div className="space-y-4">
      <Card title={t('nav.support')}>
        <form className="mx-auto max-w-lg space-y-3" onSubmit={handleSubmit(onSubmit)}>
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
              className="min-h-[120px] w-full rounded-lg border border-rt-border bg-rt-page px-3 py-2 text-sm outline-none ring-rt-primary/30 focus:ring-2"
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
