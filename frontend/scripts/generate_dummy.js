const fs = require('fs')
const path = require('path')

const OUT = path.join(__dirname, '..', 'data', 'DUMMY.json')
const COUNT = 10000
// Desired distribution (enterprise test fixtures)
const TARGET = {
  active: 9908,
  warning: 72,
  offline: 20,
}

const prefixes = ['TSLA','CYBR','RIVN','FD','VLV','NISS','MERC','VOLV','FORD','GM']
const models = ['Class 8 Semi','Cybertruck','R1T Delivery','F-150 Lightning','VNL 860','NV Cargo','eSprinter','Transit','Van']
const drivers = ['J. Doe','M. Star','K. West','A. Smith','B. Rogers','L. Nguyen','F. Gomez','R. Patel','S. Lee','C. Chan']
const statuses = ['active','warning','offline']

function randInt(min, max) { return Math.floor(Math.random()*(max-min+1))+min }
function pick(arr){ return arr[Math.floor(Math.random()*arr.length)] }

// San Francisco bbox used by the app
const minLat = 37.755
const maxLat = 37.800
const minLng = -122.45
const maxLng = -122.395

function randLat(){ return +(Math.random()*(maxLat-minLat)+minLat).toFixed(6) }
function randLng(){ return +(Math.random()*(maxLng-minLng)+minLng).toFixed(6) }

function genLog(level, msg){ return { ts: new Date().toISOString(), level, msg } }

const out = []
for(let i=1;i<=COUNT;i++){
  const prefix = pick(prefixes)
  const id = `${prefix}-${String(randInt(1,9999)).padStart(4,'0')}`
  const model = pick(models)
  const driver = Math.random() < 0.02 ? 'Offline' : pick(drivers)
  // Assign exact counts by index to produce stable distribution
  const idx = i - 1
  let status
  if (idx < TARGET.active) status = 'active'
  else if (idx < TARGET.active + TARGET.warning) status = 'warning'
  else status = 'offline'
  const battery = status === 'offline' ? 0 : randInt(0,100)
  const temp = status === 'offline' ? 0 : randInt(30,80)
  const tirePressure = status === 'offline' ? [0,0,0,0] : [randInt(30,36),randInt(30,36),randInt(30,36),randInt(30,36)]
  const cargoLoad = randInt(0,20000)
  const lat = randLat()
  const lng = randLng()

  const logs = []
  // some vehicles have a few logs
  const logCount = Math.random() < 0.2 ? randInt(1,4) : (Math.random() < 0.02 ? randInt(5,12) : 0)
  for(let j=0;j<logCount;j++){
    const p = Math.random()
    if(p<0.02) logs.push(genLog('ERROR','DEVICE_OFFLINE'))
    else if(p<0.15) logs.push(genLog('WARN','TIRE_PRESSURE_VARIANCE'))
    else logs.push(genLog('INFO','SYS_CHECK'))
  }

  out.push({ id, model, driver, status, battery, temp, tirePressure, cargoLoad, lat, lng, logs })
}

fs.writeFileSync(OUT, JSON.stringify(out, null, 2), 'utf8')
console.log(`Wrote ${COUNT} records to ${OUT}`)
