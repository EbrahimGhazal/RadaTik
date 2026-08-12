import { useTranslation } from 'react-i18next'
import { Alert } from './Alert'
import { Card } from './Card'

type Props = {
  loading: boolean
  error: string | null
  /** When false, only an error card is shown; loading is left to section-level UI. */
  showLoading?: boolean
}

/** Shared notices for `useMockDbQuery()` on dashboard-style pages. */
export function MockDbQueryStatus({ loading, error, showLoading = true }: Props) {
  const { t } = useTranslation()
  if (error) {
    return (
      <Card>
        <Alert variant="danger">{error}</Alert>
      </Card>
    )
  }
  if (loading && showLoading) {
    return (
      <Card>
        <p className="text-sm text-rt-neutral-mid">{t('common.loading')}</p>
      </Card>
    )
  }
  return null
}
