import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/Card'

export function SectionPlaceholder({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation()
  return (
    <Card title={t(titleKey)}>
      <p className="text-sm text-rt-neutral-mid">{t('pages.placeholders')}</p>
    </Card>
  )
}
