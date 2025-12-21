export type VehicleLog = { ts: string; level?: 'OK' | 'WARN' | 'ERROR' | 'INFO'; msg: string }

export type VehicleLocation = {
  lat: number
  lng: number
}

export type VehicleStatus = 'active' | 'warning' | 'danger' | 'offline' | string

export type Vehicle = {
  id: string
  model: string
  driver: string
  status: VehicleStatus
  // primary telemetry values
  fuel: number | string
  temp: number | string
  speedKph: number
  cargoLoad: number
  engineHealth: number | string

  // location
  lat: number
  lng: number

  // optional diagnostic fields (provided separately via API / SignalR)
}
