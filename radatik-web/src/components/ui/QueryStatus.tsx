import { useTranslation } from 'react-i18next'
import { Alert } from './Alert'

/** Inline loading/error for list and resource pages. */
export function QueryStatus({
  loading,
  error,
  hasData = true,
}: {
  loading: boolean
  error: string | null
  hasData?: boolean
}) {
  const { t } = useTranslation()
  if (error) return <Alert variant="danger">{error}</Alert>
  if (loading && !hasData) {
    return <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p>
  }
  return null
}
