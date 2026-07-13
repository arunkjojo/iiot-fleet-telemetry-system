"use client"
import React, { useMemo, useState, useEffect } from 'react'
import { X, Pencil } from 'lucide-react'
import { Vehicle } from '../types/vehicle'

// Fields returned by PATCH /api/vehicles/{id}, mapped into the frontend's Vehicle shape.
// Partial because the endpoint never returns/updates every Vehicle field (e.g. engineHealth).
export type VehiclePatchResult = Partial<Vehicle> & { id: string }

const DRIVER_NAME_MAX_LENGTH = 100
const DISPLAY_NUMBER_MAX_LENGTH = 30

type Props = {
  vehicle?: Vehicle | null
  onClose?: () => void
  onVehicleUpdated?: (updatedVehicle: VehiclePatchResult) => void
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

function DetailPanel({ vehicle, onClose, onVehicleUpdated }: Props) {
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

  // Inline edit state for driver name / display number (PATCH /api/vehicles/{id}, Sprint 04 UI-012)
  const [isEditing, setIsEditing] = useState(false)
  const [editDriver, setEditDriver] = useState('')
  const [editDisplayNumber, setEditDisplayNumber] = useState('')
  const [saving, setSaving] = useState(false)
  const [editError, setEditError] = useState<string | null>(null)

  // Reset edit state whenever the selected vehicle changes so a stale in-progress
  // edit for a previous vehicle never leaks onto the newly-selected one.
  useEffect(() => {
    setIsEditing(false)
    setEditError(null)
    setSaving(false)
  }, [vehicle?.id])

  const startEdit = () => {
    setEditDriver(vehicle.driver ?? '')
    setEditDisplayNumber(vehicle.displayNumber ?? '')
    setEditError(null)
    setIsEditing(true)
  }

  const cancelEdit = () => {
    setIsEditing(false)
    setEditError(null)
  }

  const handleSave = async () => {
    setEditError(null)
    const trimmedDriver = editDriver.trim()
    const trimmedDisplayNumber = editDisplayNumber.trim()

    if (!trimmedDriver || !trimmedDisplayNumber) {
      setEditError('Driver name and display number are both required.')
      return
    }
    if (trimmedDriver.length > DRIVER_NAME_MAX_LENGTH) {
      setEditError(`Driver name must be ${DRIVER_NAME_MAX_LENGTH} characters or fewer.`)
      return
    }
    if (trimmedDisplayNumber.length > DISPLAY_NUMBER_MAX_LENGTH) {
      setEditError(`Display number must be ${DISPLAY_NUMBER_MAX_LENGTH} characters or fewer.`)
      return
    }

    const body: { driverName?: string; displayNumber?: string } = {}
    if (trimmedDriver !== (vehicle.driver ?? '')) body.driverName = trimmedDriver
    if (trimmedDisplayNumber !== (vehicle.displayNumber ?? '')) body.displayNumber = trimmedDisplayNumber

    if (Object.keys(body).length === 0) {
      // nothing actually changed — no need to call the API
      setIsEditing(false)
      return
    }

    setSaving(true)
    try {
      const base = process.env.NEXT_PUBLIC_API_URL || ''
      const res = await fetch(`${base}/api/vehicles/${vehicle.id}`, {
        method: 'PATCH',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      })

      if (!res.ok) {
        if (res.status === 404) setEditError('Vehicle not found.')
        else if (res.status === 400) setEditError('Invalid input — please check the values and try again.')
        else setEditError(`Save failed (status ${res.status}).`)
        return
      }

      const data = await res.json()
      // Map the PATCH response back into the frontend's Vehicle shape, the same
      // way page.tsx's initial load normalizes fields (driver -> driver, etc).
      const updated: VehiclePatchResult = {
        id: data.id ?? vehicle.id,
        model: data.model,
        driver: data.driver,
        status: data.status,
        fuel: data.fuel,
        temp: data.temp,
        speedKph: data.speedKph,
        cargoLoad: data.cargoLoad,
        lat: data.lat,
        lng: data.lng,
        displayNumber: data.displayNumber,
      }
      onVehicleUpdated?.(updated)
      setIsEditing(false)
    } catch (e) {
      setEditError('Network error — please try again.')
    } finally {
      setSaving(false)
    }
  }

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
              {vehicle.inactive && (
                <span className="ml-2 text-[10px] font-bold tracking-wide text-slate-300 bg-white/10 px-1.5 py-0.5 rounded normal-case">Inactive</span>
              )}
            </span>
          </div>
          <div className="ml-4 flex items-center gap-1">
            {!isEditing && (
              <button
                aria-label="Edit vehicle"
                onClick={startEdit}
                className="text-slate-400 hover:text-white p-2 rounded"
              >
                <Pencil size={16} />
              </button>
            )}
            {onClose && (
              <button aria-label="Close details" onClick={onClose} className="text-slate-400 hover:text-white p-2 rounded">
                <X size={18} />
              </button>
            )}
          </div>
        </div>
        {isEditing ? (
          <div className="flex flex-col gap-3 mb-2">
            <div className="grid grid-cols-2 gap-4">
              <label className="flex flex-col gap-1">
                <span className="text-slate-500 text-xs uppercase">Driver</span>
                <input
                  className="bg-[#1c2527] p-2 rounded border border-border-dark text-white text-sm outline-none focus:border-primary"
                  value={editDriver}
                  maxLength={DRIVER_NAME_MAX_LENGTH}
                  onChange={(e) => setEditDriver(e.target.value)}
                  disabled={saving}
                />
              </label>
              <label className="flex flex-col gap-1">
                <span className="text-slate-500 text-xs uppercase">Display Number</span>
                <input
                  className="bg-[#1c2527] p-2 rounded border border-border-dark text-white text-sm outline-none focus:border-primary"
                  value={editDisplayNumber}
                  maxLength={DISPLAY_NUMBER_MAX_LENGTH}
                  onChange={(e) => setEditDisplayNumber(e.target.value)}
                  disabled={saving}
                />
              </label>
            </div>
            {editError && <p className="text-red-500 text-xs">{editError}</p>}
            <div className="flex items-center gap-2">
              <button
                onClick={handleSave}
                disabled={saving}
                className="text-xs font-bold uppercase tracking-widest bg-primary text-black px-3 py-1.5 rounded disabled:opacity-50"
              >
                {saving ? 'Saving…' : 'Save'}
              </button>
              <button
                onClick={cancelEdit}
                disabled={saving}
                className="text-xs font-bold uppercase tracking-widest text-slate-300 hover:text-white px-3 py-1.5 rounded disabled:opacity-50"
              >
                Cancel
              </button>
            </div>
          </div>
        ) : (
          <div className="grid grid-cols-2 gap-4 mb-2">
            <div className="bg-[#1c2527] p-3 rounded border border-border-dark">
              <p className="text-slate-500 text-xs uppercase">Driver</p>
              <p className="text-white font-medium">{vehicle.driver}</p>
            </div>
            <div className="bg-[#1c2527] p-3 rounded border border-border-dark">
              <p className="text-slate-500 text-xs uppercase">Model</p>
              <p className="text-white font-medium">{vehicle.model}</p>
            </div>
            {vehicle.displayNumber && (
              <div className="bg-[#1c2527] p-3 rounded border border-border-dark col-span-2">
                <p className="text-slate-500 text-xs uppercase">Display Number</p>
                <p className="text-white font-medium">{vehicle.displayNumber}</p>
              </div>
            )}
          </div>
        )}
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
