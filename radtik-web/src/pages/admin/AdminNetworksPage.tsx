import { useTranslation } from 'react-i18next'
import type { FeatureCollection } from 'geojson'
import { Card } from '../../components/ui/Card'
import { DataTable, type Column } from '../../components/ui/DataTable'
import { CoverageMap, type AccessPoint } from '../../components/maps/CoverageMap'
import { fetchResource } from '../../lib/api'
import { useMockResourceBundleQuery } from '../../hooks/useMockResourceQuery'

export function AdminNetworksPage() {
  const { t } = useTranslation()
  const { data, loading, error } = useMockResourceBundleQuery(
    'accessPoints+coverageZones',
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

  const columns: Column<AccessPoint>[] = [
    { key: 'n', header: t('networksPage.apTable'), cell: (r) => r.name },
    {
      key: 'c',
      header: t('networksPage.coordinates'),
      cell: (r) => `${r.lat.toFixed(4)}, ${r.lng.toFixed(4)}`,
    },
  ]

  return (
    <div className="space-y-4">
      <Card title={t('pages.networks')}>
        {error ? <p className="mb-6 text-sm text-red-600">{error}</p> : null}
        {!error && zones && points.length ? (
          <CoverageMap zones={zones} points={points} className="mb-6" />
        ) : !error ? (
          <p className="mb-6 text-sm text-rt-neutral-mid">
            {loading ? t('common.loading') : t('common.noData')}
          </p>
        ) : null}
        <DataTable columns={columns} rows={points} />
      </Card>
    </div>
  )
}
