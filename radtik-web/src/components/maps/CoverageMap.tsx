import { GeoJSON, MapContainer, TileLayer, CircleMarker, Popup } from 'react-leaflet'
import { useMemo } from 'react'
import type { FeatureCollection } from 'geojson'

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

/** Leaflet map: mock coverage polygon + access point markers. */
export function CoverageMap({ points, zones, className }: CoverageMapProps) {
  const center = useMemo(() => {
    const first = points[0]
    return first ? ([first.lat, first.lng] as [number, number]) : ([33.5138, 36.2765] as [number, number])
  }, [points])

  return (
    <div className={className} style={{ height: 320, width: '100%' }}>
      <MapContainer
        center={center}
        zoom={12}
        className="size-full rounded-lg"
        scrollWheelZoom={false}
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OSM</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />
        <GeoJSON
          data={zones}
          style={{
            color: '#2563eb',
            weight: 2,
            fillColor: '#06b6d4',
            fillOpacity: 0.12,
          }}
        />
        {points.map((ap) => (
          <CircleMarker
            key={ap.id}
            center={[ap.lat, ap.lng]}
            radius={10}
            pathOptions={{
              color: '#1e3a8a',
              fillColor: '#06b6d4',
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
