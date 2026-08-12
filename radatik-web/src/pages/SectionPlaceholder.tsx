import { useTranslation } from 'react-i18next'
import { Card } from '../components/ui/Card'
import { PageContent } from '../components/ui/PageContent'

export function SectionPlaceholder({ titleKey }: { titleKey: string }) {
  const { t } = useTranslation()
  return (
    <PageContent>
      <Card title={t(titleKey)}>
        <p className="text-sm text-rt-neutral-mid">{t('pages.placeholders')}</p>
      </Card>
    </PageContent>
  )
}
