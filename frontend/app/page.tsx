"use client"
import React, { useEffect, useState, useMemo, useCallback, useRef } from 'react'
import Sidebar from '../components/Sidebar'
import MapView from '../components/MapView'
import DetailPanel, { VehiclePatchResult } from '../components/DetailPanel'
import Header from '../components/Header'
import type { Vehicle, VehicleStatus } from '../types/vehicle'
import type { SignalRConnectionStatus } from '../components/ConnectionStatus'
import { useFilterStore } from '../store/useFilterStore'
import * as signalR from '@microsoft/signalr'
import { useNotificationStore } from '../store/useNotificationStore'
import Toast from '../components/Toast'

const API_URL = process.env.NEXT_PUBLIC_API_URL || ''

// Client-side-only "inactive vehicle" concept: sustained speedKph == 0 for 60+ seconds.
// Purely additive/display-only — never modifies the server-side `status` enum.
const INACTIVE_THRESHOLD_MS = 60_000

export default function Page() {
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [selected, setSelected] = useState<Vehicle | null>(null)
  const addNotif = useNotificationStore((s) => s.add)
  const [toast, setToast] = useState<{ id: string; message: string; level: string } | null>(null)
  const [connectionStatus, setConnectionStatus] = useState<SignalRConnectionStatus>('disconnected')
  const vehiclesMap = useRef<Map<string, Vehicle>>(new Map())
  const connRef = useRef<signalR.HubConnection | null>(null)
  // Last time (Date.now() ms) each vehicle was observed with speedKph > 0.
  // Updated on every SignalR update but the O(10k) inactive recompute happens
  // only in the 5s sweep below — never inline in the SignalR handler.
  const lastMovedAtMs = useRef<Map<string, number>>(new Map())
  const mountTimeMs = useRef<number>(Date.now())

  // Compute alerts once; memoized to avoid recomputing on unrelated UI changes.
  const alerts = useMemo(() => vehicles.filter((v: any) => {
    // alert conditions: explicit warning/danger statuses or threshold breaches
    const fuelLow = Number(v.fuel ?? 0) < 20
    const tempHigh = Number(v.temp ?? 0) > 65
    const speedHigh = Number(v.speedKph ?? 0) > 80
    const engineHigh = Number(v.engineHealth ?? 0) > 85
    return v.status === 'danger' || fuelLow || tempHigh || speedHigh || engineHigh
  }), [vehicles])

  const selectedStatuses = useFilterStore((s) => s.selectedStatuses)

  // Map vehicles to display on the map. If 'all' is selected, show all vehicles; otherwise show only selected statuses.
  const mapVehicles = useMemo(() => {
    if (selectedStatuses.includes('all')) return vehicles
    const setSel = new Set(selectedStatuses as VehicleStatus[])
    return vehicles.filter((v) => setSel.has(v.status as VehicleStatus))
  }, [vehicles, selectedStatuses])

  useEffect(() => {
    let mounted = true

    const load = async () => {
      try {
        const res = await fetch(`${API_URL}/api/vehicles`)
        if (!res.ok) return
        const list = await res.json()
        // normalize shape expected by UI
        const arr: Vehicle[] = (list || []).map((v: any) => ({
          id: v.id,
          model: v.model ?? v.model,
          driver: v.driver ?? v.driver,
          status: v.status ?? 'active',
          fuel: v.fuel ?? v.fuel,
          temp: v.temp ?? v.temp,
          speedKph: v.speedKph ?? v.speedKph ?? 0,
          cargoLoad: v.cargoLoad ?? v.cargoLoad ?? 0,
          engineHealth: v.engineHealth ?? v.engineHealth ?? 100,
          lat: v.lat ?? v.latitude ?? 0,
          lng: v.lng ?? v.longitude ?? 0,
          lastSeenAtUtc: v.lastSeenAtUtc
        }))

        if (!mounted) return
        vehiclesMap.current = new Map(arr.map((v) => [v.id, v]))
        // Seed every vehicle's last-moved timestamp with the mount time so nothing
        // is marked inactive before any SignalR message has arrived.
        for (const v of arr) lastMovedAtMs.current.set(v.id, mountTimeMs.current)
        setVehicles(Array.from(vehiclesMap.current.values()))

        // start SignalR connection
        const conn = new signalR.HubConnectionBuilder()
          .withUrl(`${API_URL}/fleethub`, { skipNegotiation: false })
          .withAutomaticReconnect()
          .build()

        conn.on('ReceiveFleetUpdate', (updates: any) => {
          if (!updates || !Array.isArray(updates)) return
          for (const u of updates) {
            const id = u.Id ?? u.id
            if (!id) continue
            const veh = vehiclesMap.current.get(id)
            if (!veh) continue
            const prevStatus = veh.status
            if (u.Latitude !== undefined || u.latitude !== undefined) veh.lat = u.Latitude ?? u.latitude
            if (u.Longitude !== undefined || u.longitude !== undefined) veh.lng = u.Longitude ?? u.longitude
            if (u.FuelPercent !== undefined || u.fuelPercent !== undefined || u.fuel !== undefined) veh.fuel = Math.round((u.FuelPercent ?? u.fuelPercent ?? u.fuel) * 100) / 100
            if (u.SpeedKph !== undefined || u.speedKph !== undefined || u.speed !== undefined) veh.speedKph = Math.round(u.SpeedKph ?? u.speedKph ?? u.speed)
            // Track last-moved timestamp only; the O(10k) inactive recompute + flush
            // happens in the separate 5s sweep effect, not here (this fires up to 2x/sec/vehicle).
            if (veh.speedKph > 0) lastMovedAtMs.current.set(veh.id, Date.now())
            if (u.EngineHealth !== undefined || u.engineHealth !== undefined) veh.engineHealth = u.EngineHealth ?? u.engineHealth
            // respect server-provided status when present, otherwise fall back to heuristic
            if (u.Status !== undefined || u.status !== undefined) {
              veh.status = (u.Status ?? u.status)
            } else {
              const fuelVal = Number(veh.fuel ?? 0)
              if (fuelVal < 15) veh.status = 'danger'
              else if (fuelVal < 35) veh.status = 'warning'
              else veh.status = veh.status ?? 'active'
            }
            // if status changed, add a notification and show a toast

            const fuelLow = Number(veh.fuel ?? 0) < 20
            const tempHigh = Number(veh.temp ?? 0) > 65
            const speedHigh = Number(veh.speedKph ?? 0) > 80
            const engineHigh = Number(veh.engineHealth ?? 0) > 85

            if (prevStatus !== veh.status && veh.status !== 'active' && (fuelLow || tempHigh || speedHigh || engineHigh)) {
              let errorMessage = fuelLow ? 'low fuel, ' : '';
              errorMessage += tempHigh ? 'temperature high, ' : '';
              errorMessage += speedHigh ? 'high speed, ' : '';
              errorMessage += engineHigh ? 'low engine health, ' : '';
              errorMessage = errorMessage.replace(/, $/, '') // remove trailing comma and space
              const msg = `Vehicle ${veh.id}: ${errorMessage}`
              const level = veh.status === 'danger' ? 'error' : veh.status === 'warning' ? 'warn' : 'info'

              const n = addNotif({ message: msg, level })
              setToast({ id: n.id, message: msg, level })
            }
          }
          // push a new array to re-render
          setVehicles(Array.from(vehiclesMap.current.values()))
        })

        conn.onreconnecting(() => setConnectionStatus('reconnecting'))
        conn.onreconnected(() => setConnectionStatus('connected'))
        conn.onclose(() => setConnectionStatus('disconnected'))

        await conn.start()
        connRef.current = conn
        setConnectionStatus('connected')
      } catch (e) {
        // ignore connection errors; UI remains functional with initial data
        setConnectionStatus('disconnected')
        console.error(e)
      }
    }

    load()

    return () => { mounted = false; connRef.current?.stop().catch(()=>{}) }
  }, [])

  // 5s sweep: recompute `inactive` for every vehicle and flush a single state update.
  // Kept separate from the SignalR handler so the O(10k) scan runs at most 12x/minute
  // instead of on every incoming update batch.
  useEffect(() => {
    const sweep = () => {
      const now = Date.now()
      let changed = false
      for (const v of vehiclesMap.current.values()) {
        const lastMoved = lastMovedAtMs.current.get(v.id) ?? mountTimeMs.current
        const inactive = (now - lastMoved) > INACTIVE_THRESHOLD_MS
        if (v.inactive !== inactive) {
          v.inactive = inactive
          changed = true
        }
      }
      if (changed) setVehicles(Array.from(vehiclesMap.current.values()))
    }
    const intervalId = setInterval(sweep, 5000)
    return () => clearInterval(intervalId)
  }, [])

  const handleSelect = useCallback((v: Vehicle) => setSelected(v), [])

  // Merges a PATCH /api/vehicles/{id} response (from DetailPanel's edit UI) back
  // into the live vehicle map/state, and into `selected` if that vehicle is open.
  const handleVehicleUpdated = useCallback((updatedFields: VehiclePatchResult) => {
    const existing = vehiclesMap.current.get(updatedFields.id)
    if (!existing) return
    const merged: Vehicle = { ...existing, ...updatedFields }
    vehiclesMap.current.set(updatedFields.id, merged)
    setVehicles(Array.from(vehiclesMap.current.values()))
    setSelected((prev) => (prev && prev.id === updatedFields.id ? merged : prev))
  }, [])

  return (
    <div className="h-screen flex flex-col">
      <Header connectionStatus={connectionStatus} />
      <Toast item={toast} onDone={() => setToast(null)} />

      <div className="flex flex-1 overflow-hidden relative">
        <Sidebar vehicles={vehicles} onSelect={handleSelect} selectedId={selected?.id} />
        <MapView vehicles={mapVehicles} onSelect={handleSelect} selectedId={selected?.id} />
        {selected && (
          <DetailPanel
            vehicle={selected}
            onClose={() => setSelected(null)}
            onVehicleUpdated={handleVehicleUpdated}
          />
        )}
      </div>
    </div>
  )
}
