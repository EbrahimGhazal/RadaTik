import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Button } from '../../components/ui/Button'
import { Card } from '../../components/ui/Card'
import { FieldLabel, Input, Textarea } from '../../components/ui/Input'
import { PageContent } from '../../components/ui/PageContent'

export function ClientSupportPage() {
  const { t } = useTranslation()
  const { register, handleSubmit, reset } = useForm<{ subject: string; message: string }>()

  const onSubmit = () => {
    reset()
    alert(t('client.ticket.sent'))
  }

  return (
    <PageContent>
      <Card title={t('nav.support')}>
        <form className="mx-auto max-w-lg space-y-4" onSubmit={handleSubmit(onSubmit)}>
          <div>
            <FieldLabel htmlFor="support-subject">{t('client.ticket.subject')}</FieldLabel>
            <Input id="support-subject" {...register('subject', { required: true })} />
          </div>
          <div>
            <FieldLabel htmlFor="support-message">{t('client.ticket.message')}</FieldLabel>
            <Textarea id="support-message" {...register('message', { required: true })} />
          </div>
          <Button type="submit">{t('client.ticket.send')}</Button>
        </form>
      </Card>
    </PageContent>
  )
}
