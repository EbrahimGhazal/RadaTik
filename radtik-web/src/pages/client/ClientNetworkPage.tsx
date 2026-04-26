import { useTranslation } from 'react-i18next'
import type { FeatureCollection } from 'geojson'
import { Card } from '../../components/ui/Card'
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
    <div className="space-y-4">
      <Card title={t('nav.myNetwork')}>
        <p className="mb-4 text-sm text-rt-neutral-mid">{t('pages.networks')}</p>
        {error ? <p className="text-sm text-red-600">{error}</p> : null}
        {!error && zones && points.length ? (
          <CoverageMap zones={zones} points={points} />
        ) : !error ? (
          <p className="text-sm text-rt-neutral-mid">
            {loading ? t('common.loading') : t('common.noData')}
          </p>
        ) : null}
      </Card>
    </div>
  )
}
