const fs = require('fs')
const path = require('path')

const src = path.resolve(__dirname, '..', 'data', 'DUMMY.json')
const out = path.resolve(__dirname, '..', 'data', 'DUMMY_backend.json')

function transform(entry) {
  return {
    id: entry.id || entry.Id || `VEH-${Math.floor(Math.random()*10000)}`,
    model: entry.model || entry.Model || 'NV Cargo',
    driver: entry.driver || entry.DriverName || entry.driverName || 'Unknown',
    status: entry.status || entry.Status || 'active',
    fuel: Math.round(entry.fuel ?? entry.FuelPercent ?? 50),
    temp: Math.round(entry.temp ?? entry.Temp ?? 50),
    speedKph: Math.round(entry.speadKph ?? entry.speedKph ?? entry.SpeedKph ?? 60),
    cargoLoad: Math.round(entry.cargoLoad ?? entry.CargoLoad ?? 0),
    lat: entry.lat ?? entry.Latitude ?? entry.latitude ?? 37.77,
    lng: entry.lng ?? entry.Longitude ?? entry.longitude ?? -122.41,
    engineHealth: entry.EngineHealth ?? entry.engineHealth ?? 90,
    logs: entry.logs && entry.logs.length ? entry.logs : [
      { ts: new Date().toISOString(), level: 'INFO', msg: 'SYS_CHECK' },
      { ts: new Date().toISOString(), level: 'WARN', msg: 'TIRE_PRESSURE_VARIANCE' }
    ]
  }
}

try {
  const raw = fs.readFileSync(src, 'utf8')
  const arr = JSON.parse(raw)
  const outArr = arr.map(transform)
  fs.writeFileSync(out, JSON.stringify(outArr, null, 2))
  console.log('Wrote', out)
} catch (err) {
  console.error(err)
  process.exit(1)
}
