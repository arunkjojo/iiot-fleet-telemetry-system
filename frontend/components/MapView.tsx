"use client"
import React, { useEffect, useMemo, useRef } from 'react'
import 'leaflet/dist/leaflet.css'
import L from 'leaflet'
import { MapContainer, TileLayer, Marker, Tooltip, useMap } from 'react-leaflet'
import { Vehicle } from '../types/vehicle'

type Props = {
  vehicles: Vehicle[]
  onSelect: (v: Vehicle) => void
  selectedId?: string
}

// Fallback view used when there are no vehicles yet (San Francisco bbox center).
const DEFAULT_CENTER: [number, number] = [37.7775, -122.4225]
const DEFAULT_ZOOM = 12

function statusColor(status: Vehicle['status']) {
  return status === 'active'
    ? '#0bda54'
    : status === 'warning'
    ? '#f59e0b'
    : status === 'danger'
    ? '#ef4444'
    : '#9ca3af'
}

// Builds a colored dot divIcon (and an optional larger pulsing-ring divIcon for the
// selected vehicle) so we never reference Leaflet's default marker png assets, which
// 404 under Next.js's bundler.
function buildIcon(statusHex: string, isSelected: boolean) {
  const ringHtml = isSelected
    ? `<div style="position:absolute;left:50%;top:50%;width:56px;height:56px;transform:translate(-50%,-50%);border-radius:9999px;background:${statusHex};opacity:0.18;" class="animate-ping"></div>`
    : ''
  const html = `
    <div style="position:relative;width:16px;height:16px;">
      ${ringHtml}
      <div style="position:absolute;left:50%;top:50%;width:16px;height:16px;transform:translate(-50%,-50%);border-radius:9999px;background:${statusHex};box-shadow:0 0 12px rgba(19,200,236,0.2);"></div>
    </div>
  `
  return L.divIcon({
    html,
    className: '',
    iconSize: [16, 16],
  })
}

// Fits the map's view to the current vehicle bounding box once on initial load so
// every marker starts inside the viewport. Falls back to the default SF center/zoom
// when there are no vehicles yet (fitBounds throws on an empty/invalid bounds).
function FitBoundsOnLoad({ vehicles }: { vehicles: Vehicle[] }) {
  const map = useMap()
  const hasFitted = useRef(false)

  useEffect(() => {
    if (hasFitted.current) return
    if (!vehicles || vehicles.length === 0) return

    const bounds = L.latLngBounds(vehicles.map((v) => [v.lat, v.lng] as [number, number]))
    if (bounds.isValid()) {
      map.fitBounds(bounds, { padding: [40, 40] })
      hasFitted.current = true
    }
  }, [vehicles, map])

  return null
}

function MapView({ vehicles, onSelect, selectedId }: Props) {
  // By default hide 'active' vehicles to reduce clutter; always include the selected vehicle.
  const visible = useMemo(() => {
    const list = vehicles || []
    return list //.filter((v) => v.status !== 'active' || v.id === selectedId)
  }, [vehicles, selectedId])

  // Memoize marker computations to avoid re-rendering markers when unrelated props change.
  // This reduces map re-paints for large datasets.
  const markers = useMemo(
    () =>
      (visible || []).map((v) => {
        const statusHex = statusColor(v.status)
        const isSelected = selectedId === v.id
        return { v, statusHex, isSelected, icon: buildIcon(statusHex, isSelected) }
      }),
    [visible, selectedId]
  )

  return (
    <main className="flex-1 relative bg-black flex flex-col">
      <MapContainer
        center={DEFAULT_CENTER}
        zoom={DEFAULT_ZOOM}
        className="w-full h-full"
        style={{ width: '100%', height: '100%' }}
      >
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution="&copy; OpenStreetMap contributors"
        />
        <FitBoundsOnLoad vehicles={visible} />
        {markers.map(({ v, icon }) => (
          <Marker
            key={v.id}
            position={[v.lat, v.lng]}
            icon={icon}
            eventHandlers={{ click: () => onSelect(v) }}
          >
            <Tooltip direction="top" offset={[0, -8]}>
              <div className="text-xs">
                {v.id} • {v.status}
                <div className="text-slate-400 text-[11px] mt-1">
                  Fuel: {v.fuel}% • Speed: {v.speedKph} km/h
                </div>
              </div>
            </Tooltip>
          </Marker>
        ))}
      </MapContainer>
    </main>
  )
}

export default React.memo(MapView)
