import { useTranslation } from 'react-i18next'
import type { FeatureCollection } from 'geojson'
import { Card } from '../../components/ui/Card'
import { PageContent } from '../../components/ui/PageContent'
import { QueryStatus } from '../../components/ui/QueryStatus'
import { CoverageMap, type AccessPoint } from '../../components/maps/CoverageMap'
import { fetchResource } from '../../lib/api'
import { useMockResourceBundleQuery } from '../../hooks/useMockResourceQuery'

export function ClientNetworkPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceBundleQuery(
    'client:accessPoints+coverageZones',
    async () => {
      const [ap, z] = await Promise.all([
        fetchResource<AccessPoint[]>('accessPoints'),
        fetchResource<FeatureCollection>('coverageZones'),
      ])
      return { points: ap, zones: z }
    },
  )
  const points = data?.points ?? []
  const zones = data?.zones ?? null

  return (
    <PageContent>
      <Card title={t('nav.myNetwork')}>
        <p className="mb-4 text-sm text-rt-neutral-mid">{t('pages.networks')}</p>
        <div className="mb-4">
          <QueryStatus loading={loading} error={error} hasData={points.length > 0} />
        </div>
        {!error && zones && points.length ? (
          <CoverageMap zones={zones} points={points} />
        ) : !error && !loading ? (
          <p className="text-sm text-rt-neutral-mid">{t('common.noData')}</p>
        ) : null}
      </Card>
    </PageContent>
  )
}
