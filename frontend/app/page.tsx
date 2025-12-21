"use client"
import React, { useEffect, useState, useMemo, useCallback, useRef } from 'react'
import Sidebar from '../components/Sidebar'
import MapView from '../components/MapView'
import DetailPanel from '../components/DetailPanel'
import type { Vehicle, VehicleStatus } from '../types/vehicle'
import { useFilterStore } from '../store/useFilterStore'
import * as signalR from '@microsoft/signalr'
import { useNotificationStore } from '../store/useNotificationStore'
import Toast from '../components/Toast'

const API_URL = process.env.NEXT_PUBLIC_API_URL || ''

export default function Page() {
  const [vehicles, setVehicles] = useState<Vehicle[]>([])
  const [selected, setSelected] = useState<Vehicle | null>(null)
  const addNotif = useNotificationStore((s) => s.add)
  const [toast, setToast] = useState<{ id: string; message: string; level: string } | null>(null)
  const vehiclesMap = useRef<Map<string, Vehicle>>(new Map())
  const connRef = useRef<signalR.HubConnection | null>(null)

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
          lng: v.lng ?? v.longitude ?? 0
        }))

        if (!mounted) return
        vehiclesMap.current = new Map(arr.map((v) => [v.id, v]))
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

        await conn.start()
        connRef.current = conn
      } catch (e) {
        // ignore connection errors; UI remains functional with initial data
        console.error(e)
      }
    }

    load()

    return () => { mounted = false; connRef.current?.stop().catch(()=>{}) }
  }, [])

  const handleSelect = useCallback((v: Vehicle) => setSelected(v), [])

  return (
    <div className="h-screen flex flex-col">
      <Toast item={toast} onDone={() => setToast(null)} />

      <div className="flex flex-1 overflow-hidden relative">
        <Sidebar vehicles={vehicles} onSelect={handleSelect} selectedId={selected?.id} />
        <MapView vehicles={mapVehicles} onSelect={handleSelect} selectedId={selected?.id} />
        {selected && <DetailPanel vehicle={selected} onClose={() => setSelected(null)} />}
      </div>
    </div>
  )
}
