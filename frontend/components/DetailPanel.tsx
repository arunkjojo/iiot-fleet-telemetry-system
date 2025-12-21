"use client"
import React, { useMemo, useState, useEffect } from 'react'
import { X } from 'lucide-react'
import { Vehicle } from '../types/vehicle'

type Props = {
  vehicle?: Vehicle | null
  onClose?: () => void
}

const statusColor = (s?: string) => {
  if (s === 'active') return 'bg-[#0bda54]'
  if (s === 'warning') return 'bg-yellow-500'
  if (s === 'danger') return 'bg-red-500'
  return 'bg-gray-400'
}

const chartColorByStatus = (s?: string) => {
  if (s === 'active') return '#0bda54'
  if (s === 'warning') return '#f59e0b'
  if (s === 'danger') return '#ef4444'
  return '#9ca3af'
}

function CircularChart({ value, max, color, label, display }: { value: number; max: number; color: string; label: string; display: string }) {
  const pct = Math.max(0, Math.min(100, (value / Math.max(1, max)) * 100))
  const dash = 283 - (pct / 100) * 283
  return (
    <div className="flex flex-col items-center gap-2">
      <div className="relative w-24 h-24">
        <svg className="w-full h-full transform -rotate-90" viewBox="0 0 100 100" aria-hidden>
          <defs>
            <linearGradient id={`g-${label}`} x1="0%" x2="100%">
              <stop offset="0%" stopColor={color} stopOpacity="0.95" />
              <stop offset="100%" stopColor={color} stopOpacity="0.75" />
            </linearGradient>
          </defs>
          <circle cx="50" cy="50" r="45" stroke="#162827" strokeWidth="8" fill="none" />
          <circle cx="50" cy="50" r="45" stroke={`url(#g-${label})`} strokeDasharray="283" strokeDashoffset={dash} strokeLinecap="round" strokeWidth="8" fill="none" style={{ transition: 'stroke-dashoffset 600ms ease' }} />
        </svg>
        <div className="absolute inset-0 flex flex-col items-center justify-center">
          <div className="text-sm font-extrabold text-white leading-none">{display}</div>
          <div className="text-[10px] text-slate-400 uppercase">{label}</div>
        </div>
      </div>
    </div>
  )
}

function DetailPanel({ vehicle, onClose }: Props) {
  if (!vehicle) {
    return (
      <aside className="w-96 flex flex-col border-l border-border-dark bg-surface-dark z-20 shrink-0 p-6">
        <h2 className="text-xl font-bold">Select a vehicle</h2>
        <p className="text-slate-400 mt-2 text-sm">Choose a vehicle from the left list or the map to view telemetry.</p>
      </aside>
    )
  }

  // Fetch logs from API (logs are not part of the Vehicle shape; they come from /api/vehicles/{id}/logs)
  const [logs, setLogs] = useState<{ ts: string; level?: string; msg: string; tsText?: string }[]>([])
  useEffect(() => {
    if (!vehicle) return
    const fetchLogs = async () => {
      try {
        const base = process.env.NEXT_PUBLIC_API_URL || ''
        const res = await fetch(`${base}/api/vehicles/${vehicle.id}/logs`)
        if (!res.ok) return
        const data = await res.json()
        setLogs((data || []).map((l: any) => ({ ...l, tsText: new Date(l.ts).toLocaleTimeString() })))
      } catch (e) {
        // ignore
      }
    }
    fetchLogs()
  }, [vehicle?.id])

  // derive UI-friendly values from backend shape
  const fuel = Number(vehicle.fuel ?? 0)

  // metrics to display as circular charts (value, max, unit)
  const metrics = useMemo(() => {
    return [
      { key: 'fuel', label: 'Fuel', value: Number(vehicle.fuel ?? fuel), max: 100, unit: '%', display: `${vehicle.fuel ?? fuel}%` },
      { key: 'temp', label: 'Temp', value: Number(vehicle.temp ?? 0), max: 120, unit: '°C', display: `${vehicle.temp}°` },
      { key: 'speed', label: 'Speed', value: Number(vehicle.speedKph ?? 0), max: 140, unit: 'km/h', display: `${vehicle.speedKph} km/h` },
      { key: 'cargo', label: 'Cargo', value: Number(vehicle.cargoLoad ?? 0), max: 2000, unit: 'lbs', display: `${vehicle.cargoLoad ?? 0} lbs` },
      { key: 'health', label: 'Engine', value: Number(vehicle.engineHealth ?? 0), max: 100, unit: '%', display: `${vehicle.engineHealth ?? 0}%` },
    ]
  }, [vehicle, fuel])

  return (
    <aside className="w-96 flex flex-col border-l border-border-dark bg-surface-dark z-20 shrink-0 overflow-y-auto">
      <div className="p-6 border-b border-border-dark">
        <div className="flex justify-between items-start mb-4">
          <div>
            <h2 className="text-2xl font-bold text-white tracking-tight">{vehicle.id}</h2>
            <span className="text-primary text-xs font-bold tracking-widest uppercase flex items-center gap-1 mt-1">
              <span className={`w-1.5 h-1.5 rounded-full ${statusColor(vehicle.status)} animate-pulse`} />
              {vehicle.status === 'active' ? 'Online • Transmitting' : vehicle.status === 'warning' ? 'Warning' : 'Offline'}
            </span>
          </div>
          {onClose && (
            <div className="ml-4">
              <button aria-label="Close details" onClick={onClose} className="text-slate-400 hover:text-white p-2 rounded">
                <X size={18} />
              </button>
            </div>
          )}
        </div>
        <div className="grid grid-cols-2 gap-4 mb-2">
          <div className="bg-[#1c2527] p-3 rounded border border-border-dark">
            <p className="text-slate-500 text-xs uppercase">Driver</p>
            <p className="text-white font-medium">{vehicle.driver}</p>
          </div>
          <div className="bg-[#1c2527] p-3 rounded border border-border-dark">
            <p className="text-slate-500 text-xs uppercase">Model</p>
            <p className="text-white font-medium">{vehicle.model}</p>
          </div>
        </div>
      </div>
      <div className="p-6 flex flex-col gap-6">
        <h3 className="text-slate-400 text-xs font-bold tracking-widest uppercase">System Health</h3>
        <div className="grid grid-cols-3 gap-4">
          {metrics.map((m) => (
            <CircularChart key={m.key} value={m.value} max={m.max} color={chartColorByStatus(vehicle.status)} label={m.label} display={m.display} />
          ))}
        </div>
      </div>

      <div className="flex-1 bg-black border-t border-border-dark p-4 flex flex-col min-h-[200px]">
        <div className="flex justify-between items-center mb-2">
          <h3 className="text-slate-500 text-xs font-bold tracking-widest uppercase">Live Telemetry Log</h3>
        </div>
        <div className="font-mono text-xs overflow-y-auto h-full flex flex-col gap-1 text-opacity-80">
          {logs.map((l, i) => (
            <p key={i} className="text-slate-500">[{l.tsText}] <span className={l.level === 'WARN' ? 'text-yellow-500' : l.level === 'OK' ? 'text-green-500' : 'text-primary'}>{l.level || 'INFO'}</span> {l.msg}</p>
          ))}
        </div>
      </div>
    </aside>
  )
}

export default React.memo(DetailPanel)
