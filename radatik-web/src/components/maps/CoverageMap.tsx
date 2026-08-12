import { GeoJSON, MapContainer, TileLayer, CircleMarker, Popup } from 'react-leaflet'
import { useMemo } from 'react'
import type { FeatureCollection } from 'geojson'
import { useUiStore } from '../../store/uiStore'

export interface AccessPoint {
  id: string
  name: string
  lat: number
  lng: number
}

interface CoverageMapProps {
  zones: FeatureCollection
  points: AccessPoint[]
  className?: string
}

function cssColor(token: string, fallback: string): string {
  if (typeof window === 'undefined') return fallback
  const value = getComputedStyle(document.documentElement).getPropertyValue(token).trim()
  return value || fallback
}

/** Leaflet map: coverage polygon + access point markers — colors from design tokens. */
export function CoverageMap({ points, zones, className }: CoverageMapProps) {
  const theme = useUiStore((s) => s.theme)
  const center = useMemo(() => {
    const first = points[0]
    return first ? ([first.lat, first.lng] as [number, number]) : ([33.5138, 36.2765] as [number, number])
  }, [points])

  const colors = useMemo(
    () => ({
      stroke: cssColor('--color-rt-primary', '#2563eb'),
      fill: cssColor('--color-rt-logo-cyan', '#06b6d4'),
      markerStroke: cssColor('--color-rt-primary-dark', '#1d4ed8'),
    }),
    [theme],
  )

  return (
    <div className={className} style={{ height: 320, width: '100%' }}>
      <MapContainer
        center={center}
        zoom={12}
        className="size-full rounded-xl"
        scrollWheelZoom={false}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <GeoJSON
          data={zones}
          style={{
            color: colors.stroke,
            weight: 2,
            fillColor: colors.fill,
            fillOpacity: 0.12,
          }}
        />
        {points.map((ap) => (
          <CircleMarker
            key={ap.id}
            center={[ap.lat, ap.lng]}
            radius={10}
            pathOptions={{
              color: colors.markerStroke,
              fillColor: colors.fill,
              fillOpacity: 0.85,
            }}
          >
            <Popup>{ap.name}</Popup>
          </CircleMarker>
        ))}
      </MapContainer>
    </div>
  )
}
