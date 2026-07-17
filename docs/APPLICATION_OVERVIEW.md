# Application Overview

> What this system is, how its pieces fit together, and how a telemetry reading travels from source to screen. For the formal spec, see [`docs/requirements/REQUIREMENTS.md`](requirements/REQUIREMENTS.md); for the sprint-by-sprint build history and process, see [`docs/SDD_WORKFLOW.md`](SDD_WORKFLOW.md).

---

## 1. What is this system

The IIoT Fleet Telemetry System is a real-time monitoring dashboard for 10,000+ industrial vehicles. It streams live telemetry — GPS position, fuel level, speed, temperature, engine health, cargo load — from a simulated (or, in live mode, real) fleet to a web dashboard, updating each vehicle's state roughly twice a second. Operators use it to see fleet-wide status at a glance, get alerted when a vehicle crosses a danger/warning threshold, search/filter the fleet, and drill into any single vehicle's live gauges and recent event log.

The system's own status logic (`active`/`warning`/`danger`/`offline`) is deterministic — computed from raw metric thresholds, not manually set — so the dashboard's colors and counts always reflect the same rules documented in `REQUIREMENTS.md` §4.1, whether the data came from the built-in simulator or a real ingestion feed.

## 2. Topology

```
┌─────────────┐        ┌──────────────┐        ┌───────────────┐
│  frontend   │◄──────►│   backend    │◄──────►│  PostgreSQL    │
│  Next.js 15 │  HTTP  │ ASP.NET Core │  EF     │  (db)          │
│  :3000      │  +     │  8 Web API   │  Core   │  :5432         │
└─────────────┘  Sig-  │  :8080       │        └───────────────┘
                 nalR   └──────┬───────┘
                                │ HTTP POST (live mode only)
                        ┌───────┴────────┐
                        │  iiot-emitter  │
                        │  Python client │
                        └────────────────┘
```

- **Frontend** (`frontend/`) — Next.js 15 App Router dashboard. Opens one SignalR connection per session (`frontend/app/page.tsx`), holds live vehicle state in a `useRef<Map>` for O(1) per-vehicle updates, and renders the sidebar, map, and detail panel from that state via Zustand stores.
- **Backend** (`backend/`) — ASP.NET Core 8 Web API. Owns the SignalR hub (`/fleethub`), the REST endpoints under `/api/`, the Swagger UI (`/swagger`), and — depending on mode — either the in-memory dummy simulation or the live-ingestion pipeline.
- **Database** — PostgreSQL, reached via EF Core (`FleetDbContext`). Stores the `vehicles`, `telemetry_snapshots`, and `vehicle_logs` tables (see `REQUIREMENTS.md` §6).
- **iiot-emitter** — a standalone Python client, active only in live mode, that POSTs synthetic-but-externally-sourced telemetry to the backend's ingest endpoint, simulating what a real fleet's edge devices would do.

All services communicate over the Docker Compose network `iiot-fleet-net` (see [`docs/devops-learn/Docker_Compose.md`](devops-learn/Docker_Compose.md) for how that's wired) or, in Kubernetes, via the `Service` objects the Helm chart renders (see [`docs/devops-learn/K8s.md`](devops-learn/K8s.md) and [`docs/HELM_GUIDE.md`](HELM_GUIDE.md)).

## 3. The USE_LIVE_TELEMETRY branch point

The single most important fork in this codebase is the `USE_LIVE_TELEMETRY` environment variable (default `false`). It decides which of two completely different data-flow paths powers the dashboard:

- **`false` (dummy mode)** — the backend generates its own fleet in-memory and simulates movement/telemetry drift every tick. No emitter, no ingest traffic, no persistence required for the live view.
- **`true` (live mode)** — the backend instead sources current vehicle state from telemetry actually POSTed to it (by `iiot-emitter` or any other client), computed status server-side per-reading, and persisted.

Both paths converge on the same SignalR broadcast to the frontend, so the dashboard code itself doesn't need to know which mode is active.

## 4. Data flow — dummy mode (default)

1. `TelemetrySimulationService` (a `BackgroundService`) seeds ~10,000 vehicles in memory at startup, each assigned to a synthetic road "corridor" for movement.
2. On a ~1-second tick, it perturbs every vehicle's position, fuel, speed, temperature, and engine health, then evaluates status from those metrics via its own `EvaluateStatus` (offline → danger → warning → active priority, matching `REQUIREMENTS.md` §4.1's rules).
3. Every ~20 seconds, a rebalance pass nudges the fleet-wide status distribution back toward realistic ranges (see `REQUIREMENTS.md` §4.2) by moving a randomly-chosen subset of vehicles between statuses.
4. The resulting `VehicleUpdate[]` batch is broadcast via the SignalR hub (`backend/Hubs/FleetHub.cs`, method `ReceiveFleetUpdate`), MessagePack-serialized for low latency.
5. The frontend's SignalR handler (`frontend/app/page.tsx`) merges each update into its `vehiclesMap` ref and flushes a new `vehicles` array to React state, which the Sidebar/MapView/DetailPanel components consume.

## 5. Data flow — live mode (`USE_LIVE_TELEMETRY=true`)

1. `iiot-emitter` fetches the real vehicle roster from `GET /api/vehicles/metadata` (never invents vehicle IDs) and periodically POSTs one reading per vehicle to `POST /api/telemetry/ingest`.
2. `TelemetryIngestController` validates the payload, computes status via `VehicleStatusEvaluator.Evaluate` (the canonical, live-mode-only evaluator — intentionally a separate implementation from the simulator's, see the doc-comment on that class), and updates `LiveTelemetryStore`, an in-memory current-state cache. It never calls `SaveChangesAsync` synchronously — writes are enqueued to a buffered writer instead, so ingest requests stay fast.
3. `TelemetryPersistenceService` drains that buffer and writes `telemetry_snapshots`/`vehicle_logs` rows to PostgreSQL in the background.
4. `LiveBroadcastService` picks up current state from `LiveTelemetryStore` and broadcasts it over the same `/fleethub` SignalR hub the dummy-mode path uses — same wire format, same frontend handler.
5. Separately, `TelemetryRetentionService` runs on a schedule and deletes `telemetry_snapshots` rows older than a configurable retention window, in batches, so the table doesn't grow unbounded.

## 6. Where status and alerts are decided

Two distinct things are easy to conflate:

- **`status`** (`active`/`warning`/`danger`/`offline`) — computed **server-side**, deterministically, from the thresholds in `REQUIREMENTS.md` §4.1. This is what colors the sidebar dots and map markers.
- **Frontend alert thresholds** (`REQUIREMENTS.md` §4.3) — a *separate*, looser set of conditions (fuel < 20%, temp > 65°C, speed > 80 kph, engineHealth < 15) that the frontend checks independently to decide when to fire a toast notification. A vehicle can be server-side `active` and still trigger a frontend alert, or vice versa — the two systems are intentionally decoupled.

## 7. Glossary / file index

| Concept | Where to look |
|---------|---------------|
| Agent roles, write-scopes, service topology | [`AGENTS.md`](../AGENTS.md) |
| Full functional/non-functional spec | [`docs/requirements/REQUIREMENTS.md`](requirements/REQUIREMENTS.md) |
| Sprint-by-sprint build process | [`docs/SDD_WORKFLOW.md`](SDD_WORKFLOW.md) |
| Docker/Compose concepts + this project's usage | [`docs/devops-learn/Docker_Compose.md`](devops-learn/Docker_Compose.md) |
| Helm concepts + this project's chart | [`docs/devops-learn/Helm.md`](devops-learn/Helm.md) |
| Kubernetes concepts + this project's chart | [`docs/devops-learn/K8s.md`](devops-learn/K8s.md) |
| Helm install/upgrade operational steps | [`docs/HELM_GUIDE.md`](HELM_GUIDE.md) |
| Dummy-mode simulation | `backend/Services/TelemetrySimulationService.cs` |
| Live-mode status evaluation | `backend/Services/VehicleStatusEvaluator.cs` |
| Live-mode ingest endpoint | `backend/Controllers/TelemetryIngestController.cs` |
| SignalR hub | `backend/Hubs/FleetHub.cs` (`/fleethub`) |
| Frontend SignalR entry point + state | `frontend/app/page.tsx` |
