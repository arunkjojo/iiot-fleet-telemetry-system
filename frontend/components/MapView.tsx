"use client"
import React, { useMemo } from 'react'
import { Vehicle } from '../types/vehicle'

type Props = {
  vehicles: Vehicle[]
  onSelect: (v: Vehicle) => void
  selectedId?: string
}

// Simple mapper to transform lat/lng into percentage positions inside a box
function project(lat: number, lng: number) {
  // Use San Francisco bbox center heuristics from sample data
  const minLat = 37.755
  const maxLat = 37.800
  const minLng = -122.45
  const maxLng = -122.395

  const y = (1 - (lat - minLat) / (maxLat - minLat)) * 100
  const x = ((lng - minLng) / (maxLng - minLng)) * 100
  // clamp to 0-100% so markers remain inside the map image bounds
  const clamp = (v: number) => Math.max(0, Math.min(100, v))
  return { left: `${clamp(x)}%`, top: `${clamp(y)}%` }
}

function MapView({ vehicles, onSelect, selectedId }: Props) {
  // By default hide 'active' vehicles to reduce clutter; always include the selected vehicle.
  const visible = useMemo(() => {
    const list = vehicles || [];
    return list; //.filter((v) => v.status !== 'active' || v.id === selectedId)
  }, [vehicles, selectedId])

  // Memoize marker computations to avoid re-rendering markers when unrelated props change.
  // This reduces map re-paints for large datasets.
  const markers = useMemo(() => (visible || []).map((v) => {
    const pos = project(v.lat, v.lng)
    const statusHex = v.status === 'active' ? '#0bda54' : v.status === 'warning' ? '#f59e0b' : v.status === 'danger' ? '#ef4444' : '#9ca3af';
    const isSelected = selectedId === v.id;
    return { v, pos, statusHex, isSelected }
  }), [visible, selectedId])

  return (
    <main className="flex-1 relative bg-black flex flex-col">
      <div className="absolute inset-0 bg-cover bg-center z-0 opacity-80" style={{ backgroundImage: "linear-gradient(transparent, rgba(0,0,0,0.6)), url('https://i.pinimg.com/736x/33/63/21/3363219f117127d8423bc28d88043425.jpg')" }} />
      <div className="absolute inset-0 z-0 bg-gradient-to-b from-black/60 via-transparent to-black/60 pointer-events-none" />

      <div className="relative z-10 w-full h-full">
        <div className="relative w-full h-full">
          {markers.map(({ v, pos, statusHex, isSelected }) => (
            <div key={v.id} style={{ position: 'absolute', left: pos.left, top: pos.top, transform: 'translate(-50%, -50%)', zIndex: isSelected ? 60 : 10, opacity: v.inactive ? 0.4 : 1 }}>
              <div onClick={() => onSelect(v)} role="button" tabIndex={0} className="group cursor-pointer relative" onKeyDown={(e)=>{ if(e.key==='Enter') onSelect(v) }}>
                <div className="relative flex items-center justify-center">
                  <div aria-hidden className={`absolute rounded-full ${isSelected ? 'animate-ping' : ''}`} style={{ width: isSelected ? 56 : 0, height: isSelected ? 56 : 0, backgroundColor: statusHex, opacity: isSelected ? 0.18 : 0, transform: 'translate(-50%, -50%)', left: '50%', top: '50%' }} />
                  <div className="w-4 h-4 rounded-full shadow-[0_0_12px_rgba(19,200,236,0.2)]" style={{ backgroundColor: statusHex }} />
                </div>
                  <div className="absolute mt-6 left-1 transform -translate-x-1 hidden group-hover:block bg-surface-dark border border-border-dark px-2 py-1 rounded text-xs w-auto whitespace-nowrap z-[999999] pointer-events-auto">
                  {v.id} • {v.status}
                  <div className="text-slate-400 text-[11px] mt-1">Fuel: {v.fuel}% • Speed: {v.speedKph} km/h</div>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>
    </main>
  )
}

export default React.memo(MapView)
