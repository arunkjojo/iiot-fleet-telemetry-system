# IIoT Fleet Telemetry System — Requirements

**Version:** 0.1  
**Date:** 2026-06-29  
**Owner:** ARCH

---

## 1. Project Overview

The IIoT Fleet Telemetry System is a real-time industrial asset monitoring platform. It tracks 10,000+ vehicles simultaneously, streaming live telemetry (position, fuel, speed, temperature, engine health) to a web dashboard at up to 2 updates/second per vehicle. The system is designed for industrial operators who need immediate visibility into fleet status, alert conditions, and operational events.

---

## 2. Functional Requirements

### 2.1 Real-Time Vehicle Monitoring

| ID | Requirement |
|----|-------------|
| F-01 | The system MUST display the live status of at least 10,000 vehicles simultaneously |
| F-02 | Vehicle telemetry MUST update in the dashboard within 1 second of a state change |
| F-03 | Each vehicle MUST have: ID, driver name, model, status, GPS coordinates, fuel%, speed (kph), temperature (°C), engine health (0-100), cargo load (kg) |
| F-04 | Vehicle status MUST be one of: `active`, `warning`, `danger`, `offline` |
| F-05 | Status MUST be derived deterministically from telemetry thresholds (see section 4) |

### 2.2 Alert System

| ID | Requirement |
|----|-------------|
| F-06 | The dashboard MUST display a toast notification when a vehicle enters `warning` or `danger` status |
| F-07 | The dashboard MUST maintain a persistent notification history accessible via a modal |
| F-08 | Alerts MUST fire on threshold breaches: fuel < 20%, temp > 65°C, speed > 80 kph, engineHealth < 15 |
| F-09 | Notifications MUST support read/unread tracking and bulk-clear |

### 2.3 Vehicle Search and Filtering

| ID | Requirement |
|----|-------------|
| F-10 | The sidebar MUST support free-text search by vehicle ID or driver name |
| F-11 | Search MUST return results within 200ms for a 10,000-vehicle dataset |
| F-12 | The sidebar MUST support filtering by status: All, Active, Warning, Danger, Offline |
| F-13 | Status filter MUST show a count per status |

### 2.4 Vehicle Detail

| ID | Requirement |
|----|-------------|
| F-14 | Clicking a vehicle MUST open a detail panel showing all telemetry fields as visual gauges |
| F-15 | The detail panel MUST show the last 50 telemetry event log entries for the selected vehicle |
| F-16 | Log entries MUST include: timestamp (UTC), severity level (INFO/WARN/ERROR), message |

### 2.5 Map Visualization

| ID | Requirement |
|----|-------------|
| F-17 | The map MUST display vehicle positions color-coded by status |
| F-18 | Selecting a vehicle on the map MUST open the detail panel |
| F-19 | The map MUST update vehicle positions in real-time as telemetry updates arrive |

### 2.6 API (Backend)

| ID | Requirement |
|----|-------------|
| F-20 | `GET /api/vehicles` MUST return the current state of all vehicles as a JSON array |
| F-21 | `GET /api/vehicles/{id}` MUST return a single vehicle with its recent 50 log entries |
| F-22 | `GET /api/vehicles/{vehicleId}/logs` MUST return the last 50 log entries for a vehicle |
| F-23 | The backend MUST expose a Swagger UI at `/swagger` documenting all endpoints |
| F-24 | The backend MUST stream `VehicleUpdate[]` batches via SignalR to all connected clients |
| F-25 | `POST /api/telemetry/ingest` MUST accept a single vehicle's telemetry reading, compute its status server-side, and return `202 Accepted` |
| F-26 | The backend MUST support a `USE_LIVE_TELEMETRY` toggle; when `true`, vehicle state MUST be sourced from live ingestion instead of the in-memory dummy simulation; default `false` |
| F-27 | The Python IIoT emitter MUST only emit telemetry for vehicle IDs that already exist in the `vehicles` table, obtained via `GET /api/vehicles/metadata` |
| F-28 | `GET /api/health/signalr` MUST return the current `/fleethub` connection tracking state (at minimum: active connection count) as `200 OK` JSON |
| F-29 | *(Removed in Sprint 07 — the client-side "inactive vehicle" concept and its backing rules were removed entirely; see `F-35`.)* |
| F-30 | The backend MUST run a background retention/cleanup policy that deletes `telemetry_snapshots` rows older than a configurable retention window, without introducing new database tables |
| F-31 | `PATCH /api/vehicles/{id}` MUST accept an optional `driverName` and/or `displayNumber` and update only those fields; the `id` path parameter (primary key, FK target, and the exact string the Python emitter sources from `GET /api/vehicles/metadata`) MUST NEVER be renamed or accepted as a mutable field |
| F-32 | Dummy-mode vehicle IDs (`TelemetrySimulationService.MakeId()`) MUST use the same `VEH-NNNNN` (zero-padded 5-digit) format as live mode, not randomly-generated strings. This targets `TelemetrySimulationService.MakeId()` only — changing the ID format string does NOT violate that file's "in-memory only, no DB/HTTP calls" rule, since no dependency is added and no I/O is introduced, only the generated string's shape changes |
| F-35 | The sidebar MUST always list every vehicle matching the current search/status filter — no inactivity-based hiding and no top-N display cap (Sprint 07 removed the prior 24h-activity-filter and top-10-cap requirements this supersedes) |

---

## 3. Non-Functional Requirements

### 3.1 Performance

| ID | Requirement |
|----|-------------|
| NF-01 | The dashboard MUST render at 60 FPS when displaying 10,000+ vehicles |
| NF-02 | `GET /api/vehicles` MUST respond in under 500ms |
| NF-03 | SignalR updates MUST be broadcast within 500ms of the simulation tick |
| NF-04 | The vehicle sidebar list MUST scroll smoothly for 10,000 items (virtualization required) |
| NF-05 | Frontend state updates MUST NOT trigger full component re-renders on every SignalR message |

### 3.2 Reliability

| ID | Requirement |
|----|-------------|
| NF-06 | The SignalR connection MUST auto-reconnect on network interruption |
| NF-07 | Offline vehicles MUST auto-recover after 8 seconds in the simulation |
| NF-08 | The fleet status distribution MUST be rebalanced every ~20 seconds to maintain realistic ratios |
| NF-09 | Docker Compose health checks MUST verify service availability before dependent services start |

### 3.3 Security

| ID | Requirement |
|----|-------------|
| NF-10 | The backend MUST enforce CORS to only allow requests from configured frontend origins |
| NF-11 | No secrets (passwords, API keys) MUST be committed to source control |
| NF-12 | Connection strings MUST be injected via environment variables, not hardcoded |

### 3.4 Maintainability

| ID | Requirement |
|----|-------------|
| NF-13 | All TypeScript code MUST pass `tsc --noEmit` with zero errors before commit |
| NF-14 | All TypeScript code MUST pass ESLint with zero warnings before commit |
| NF-15 | All C# code MUST build with `dotnet build` with zero errors before commit |
| NF-16 | All commits MUST follow format: `IIOT-S{NN}-{TASK-ID}: <summary>` |

---

## 4. Business Rules

### 4.1 Vehicle Status Thresholds

Status is assigned in priority order (highest wins):

| Status | Condition |
|--------|-----------|
| `offline` | `fuelPercent < 1` OR `temp < 5` OR `engineHealth < 5` OR `speedKph < 2` |
| `danger` | `fuelPercent < 10.0` OR `speedKph > 90.0` OR `temp > 85` OR `engineHealth > 90` |
| `warning` | `(fuelPercent < 30.0 AND fuelPercent >= 10.0)` OR `(temp > 60 AND temp <= 85)` OR `(engineHealth > 60 AND engineHealth <= 90)` OR `(speedKph >= 60.0 AND speedKph <= 90.0)` |
| `active` | `30.0 <= fuelPercent <= 100.0` OR `5 <= temp <= 60` OR `5 <= engineHealth <= 60` OR `2 <= speedKph <= 60.0` (all other cases, default) |

### 4.2 Fleet Distribution Caps (Simulation)

The simulation re-rolls a random target within each range every ~20 seconds (rebalance tick) to maintain a realistic, drifting fleet distribution:

| Status | Range | Approx. @ 10,000 vehicles |
|--------|-------|---------------------------|
| offline | 40–100 | 40–100 |
| danger | 100–400 | 100–400 |
| warning | 500–800 | 500–800 |
| active | remainder | ~8,700+ |

### 4.3 Alert Thresholds (Frontend)

Frontend alerts fire independently of server-side status when:

| Metric | Threshold |
|--------|-----------|
| Fuel | < 20% |
| Temperature | > 65°C |
| Speed | > 80 kph |
| Engine Health | < 15 |

---

## 5. Data Model

### 5.1 Vehicle (in-memory, runtime)

```
id            : string    — unique vehicle identifier (e.g. "VEH-00001"), immutable primary key
driverName    : string    — driver full name (operator-editable via `PATCH /api/vehicles/{id}`)
displayNumber : string    — operator-editable "fleet number" (e.g. "FL-00001"), distinct from `id`
model         : string    — "NV Cargo" | "Apex Hauler"
status        : string    — "active" | "warning" | "danger" | "offline"
latitude      : double    — GPS latitude (San Francisco bbox: 37.70–37.81)
longitude     : double    — GPS longitude (San Francisco bbox: -122.52 to -122.35)
fuelPercent   : double    — 0.0 to 100.0
speedKph      : double    — kilometers per hour
engineHealth  : int       — 0 to 100
temp          : int       — degrees Celsius
cargoLoad     : int       — kilograms
lastSeenAtUtc : DateTime  — UTC timestamp of last telemetry activity; live mode: last ingest time, dummy mode: always current server time
```

### 5.2 VehicleUpdate (SignalR broadcast payload)

Subset of Vehicle sent every ~500ms to all connected clients:

```
id, latitude, longitude, fuelPercent, speedKph, engineHealth, status, temp
```

### 5.3 VehicleLog (event log entry)

```
ts      : DateTime  — UTC timestamp
level   : string    — "INFO" | "WARN" | "ERROR"
message : string    — human-readable event description
```

---

## 6. PostgreSQL Schema

### 6.1 vehicles

```sql
CREATE TABLE vehicles (
    id             VARCHAR(20) PRIMARY KEY,
    driver_name    VARCHAR(100) NOT NULL,
    display_number VARCHAR(30),
    model          VARCHAR(50)  NOT NULL,
    created_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);
```

`display_number` is nullable at the DB level (pre-existing rows may not have one until edited or re-seeded); `DbSeeder` populates it by default for freshly-seeded rows. Edited via `PATCH /api/vehicles/{id}`; the `id` primary key itself is never renamed (see the API/data requirement on `PATCH /api/vehicles/{id}` immutability above).

### 6.2 telemetry_snapshots

```sql
CREATE TABLE telemetry_snapshots (
    id           BIGSERIAL PRIMARY KEY,
    vehicle_id   VARCHAR(20)  NOT NULL REFERENCES vehicles(id),
    recorded_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    latitude     DOUBLE PRECISION,
    longitude    DOUBLE PRECISION,
    fuel_percent DOUBLE PRECISION,
    speed_kph    DOUBLE PRECISION,
    engine_health INT,
    temp_celsius INT,
    cargo_load   INT,
    status       VARCHAR(10)
);

CREATE INDEX idx_telemetry_vehicle_time ON telemetry_snapshots(vehicle_id, recorded_at DESC);
```

### 6.3 vehicle_logs

```sql
CREATE TABLE vehicle_logs (
    id         BIGSERIAL PRIMARY KEY,
    vehicle_id VARCHAR(20)  NOT NULL REFERENCES vehicles(id),
    logged_at  TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    level      VARCHAR(10),
    message    TEXT
);

CREATE INDEX idx_logs_vehicle_time ON vehicle_logs(vehicle_id, logged_at DESC);
```

---

## 7. API Contract

### GET /api/vehicles

**Response:** `200 OK` — `application/json`

```json
[
  {
    "id": "VEH-00001",
    "model": "NV Cargo",
    "driver": "Joy",
    "status": "active",
    "fuel": 84.5,
    "temp": 62,
    "speedKph": 45.2,
    "cargoLoad": 3200,
    "engineHealth": 72,
    "lat": 37.7749,
    "lng": -122.4194
  }
]
```

### GET /api/vehicles/{id}

**Response:** `200 OK`

```json
{
  "vehicle": { /* same shape as above */ },
  "logs": [
    { "ts": "2026-06-29T10:00:00Z", "level": "INFO", "message": "Vehicle seeded" }
  ]
}
```

### GET /api/vehicles/{vehicleId}/logs

**Response:** `200 OK` — array of VehicleLog (max 50 entries, newest first)

### GET /api/vehicles/metadata

**Response:** `200 OK` — array of `{ id, driver }` for all 10,000 vehicles

---

## 8. SignalR Protocol

**Hub URL:** `/fleethub`  
**Method name (server → client):** `ReceiveFleetUpdate`  
**Payload:** `VehicleUpdate[]` (array of partial vehicle updates)  
**Serialization:** MessagePack (binary) — negotiated via `withHubProtocol(new MessagePackHubProtocol())`  
**Broadcast frequency:** every ~500ms  
**Client reconnect:** automatic with exponential backoff

---

## 9. Environment Variables

| Variable | Service | Description |
|----------|---------|-------------|
| `NEXT_PUBLIC_API_URL` | Frontend | Base URL for backend API (e.g. `http://localhost:8080`) |
| `FRONTEND_ORIGIN` | Backend | Primary CORS-allowed origin (e.g. `http://localhost:3000`) |
| `ADDITIONAL_FRONTEND_ORIGINS` | Backend | Comma-separated additional CORS origins |
| `ConnectionStrings__Fleet` | Backend | PostgreSQL connection string |
| `ASPNETCORE_ENVIRONMENT` | Backend | `Development` or `Production` |
| `USE_LIVE_TELEMETRY` | Backend | Toggles live ingestion vs. dummy simulation as the vehicle data source; default `false` |
| `BACKEND_URL` | iiot-emitter | Base URL of the backend API the emitter posts telemetry to (e.g. `http://backend:8080`) |
| `VEHICLE_COUNT` | iiot-emitter | Number of vehicles the emitter simulates, sliced from the fetched roster; default `10000` |
| `TICK_INTERVAL_SECONDS` | iiot-emitter | Seconds between telemetry ticks per simulated vehicle; default `3` |
| `MAX_CONCURRENCY` | iiot-emitter | Maximum concurrent outbound HTTP POSTs the emitter issues; default `300` |
| `TelemetryRetention__RetentionDays` | Backend | Number of days of `telemetry_snapshots` history to retain before rows are eligible for deletion; default `30` |
| `TelemetryRetention__SweepIntervalMinutes` | Backend | Minutes between successive retention sweep runs of `TelemetryRetentionService`; default `60` |
| `TelemetryRetention__DeleteBatchSize` | Backend | Maximum number of rows deleted per batch during a retention sweep, to bound lock/IO impact; default `5000` |

---

## 10. Out of Scope (Current Version)

- User authentication and authorization
- Multi-tenant isolation
- Geofencing or route deviation alerts
- Historical analytics dashboards
- Mobile application
- WebGL/GPU-accelerated map rendering (Mapbox/Deck.gl)
- Horizontal scaling / load balancing
- Real GPS data (current implementation uses synthetic SF corridor simulation)
