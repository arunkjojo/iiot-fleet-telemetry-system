# Changelog

All notable changes to the IIoT Fleet Telemetry System are documented here.

Format: `## vX.Y.Z — YYYY-MM-DD` with sections `### Add`, `### Fix`, `### Update`.
Version is bumped once per sprint at sprint-end by the ARCH agent.

---

## v0.3.0 — 2026-07-20

### Add

- **SignalR connection-status indicator** — `frontend/components/ConnectionStatus.tsx` renders a
  colored dot + label ("Connected"/"Reconnecting"/"Disconnected") in the dashboard header, driven
  by the existing single `/fleethub` connection's `onreconnecting`/`onreconnected`/`onclose`
  events in `frontend/app/page.tsx` — no second SignalR connection is created
- **`GET /api/health/signalr` endpoint** — backed by a new thread-safe `HubConnectionTracker`
  singleton wired into `FleetHub`'s `OnConnectedAsync`/`OnDisconnectedAsync` lifecycle; returns
  `{ connectedClients, lastEventAtUtc }` regardless of `USE_LIVE_TELEMETRY` mode
- **Client-side inactive-vehicle detection + "Hide Inactive" toggle** — the frontend now tracks
  each vehicle's last-moved timestamp and marks it `inactive: true` after 60+ continuous seconds
  of `speedKph == 0`, dimming it in the Sidebar/MapView and tagging it in the DetailPanel; an
  opt-in (default off) "Hide Inactive" toggle in `useFilterStore.ts` filters the sidebar list.
  **This is a display-only, client-computed concept** — it does NOT change the `status` API field
  contract (`active`/`warning`/`danger`/`offline`), is never sent to or derived by the backend,
  and does not alter `VehicleStatusEvaluator.cs`'s status priority order. A vehicle can be
  simultaneously `status: "active"` and `inactive: true`. See REQUIREMENTS.md F-29/§4.4.
- **Telemetry retention/cleanup background service** — `backend/Services/TelemetryRetentionService.cs`
  periodically deletes `telemetry_snapshots`/`vehicle_logs` rows older than a configurable
  retention window in bounded batches (`TelemetryRetention__RetentionDays`,
  `TelemetryRetention__SweepIntervalMinutes`, `TelemetryRetention__DeleteBatchSize`), closing
  ADR-001 action item #5; runs only when `USE_LIVE_TELEMETRY=true`

### Known Issues

- `frontend/package.json` has no `lint`/`type-check` npm scripts and no ESLint config/dependency
  exists anywhere in `frontend/`, despite `frontend/AGENTS.md` and REQUIREMENTS.md NF-13/NF-14
  documenting both as required pre-commit gates. Found during this sprint's UI-010/UI-011; type
  safety was verified via `npx tsc --noEmit` instead. Tracked in `docs/sprints/BACKLOG.md` for a
  standalone fix.
- NF-01 (10k-vehicle 60 FPS render) and NF-03 (SignalR ~500ms cadence) were only validated at
  reduced scale (`VEHICLE_COUNT=300`, not the production default of 10,000) this sprint, per
  ANALYST-001's findings — sandbox constraints prevented a full-scale run. NF-02 (`GET
  /api/vehicles` <500ms) passed at reduced scale (p95 ≈ 109ms). A full-scale follow-up load test
  is recommended before relying on these numbers at 10,000 vehicles; tracked in
  `docs/sprints/BACKLOG.md`.

---

## v0.2.0 — 2026-07-09

### Add

- **Live telemetry ingestion endpoint** — `POST /api/telemetry/ingest` accepts a single
  vehicle's reading, computes its status server-side via `VehicleStatusEvaluator`, upserts the
  new `ILiveTelemetryStore`, enqueues a durable write, and returns `202 Accepted`
- **Python IIoT fleet emitter** (`iiot-emitter/emitter.py`) — simulates up to 10,000
  independent onboard devices, each an independent `asyncio` task on its own tick loop,
  posting real telemetry over HTTP to the backend; bounded by a shared connection pool and
  semaphore so emitter cardinality never translates 1:1 into backend/DB load; only ever emits
  vehicle IDs sourced from `GET /api/vehicles/metadata`
- **Live/simulation toggle** — `USE_LIVE_TELEMETRY` env var (default `false`) decides at
  backend startup whether vehicle state is sourced from the new live ingestion pipeline or the
  legacy in-memory `TelemetrySimulationService`; Docker Compose sets it to `true` by default
- **Tuned PostgreSQL image** (`db/Dockerfile`, `db/postgresql.conf`) — pins `postgres:16-alpine`
  and tunes `max_connections`, `shared_buffers`, `work_mem`, `maintenance_work_mem`,
  `effective_cache_size`, and `checkpoint_completion_target` for sustained batched-insert
  telemetry workloads; sets `listen_addresses = '*'` for sibling-container connectivity
- **Buffered DB persistence service** (`backend/Services/TelemetryPersistenceService.cs`) —
  drains bounded in-process channels of `TelemetrySnapshotEntity`/`VehicleLogEntity` in batches
  on a fixed interval, decoupling emitter/request count from DB connection count
- **Live SignalR broadcast service** (`backend/Services/LiveBroadcastService.cs`) — relays only
  vehicles that changed since the last tick from `ILiveTelemetryStore` to all `/fleethub`
  clients roughly every 500ms

### Fix

- Generated the missing EF Core migrations needed for the `vehicles`, `telemetry_snapshots`,
  and `vehicle_logs` tables to actually exist against a fresh database
- Restored the missing `frontend/public/` directory, which was blocking the `frontend` Docker
  image build (`COPY --from=builder /app/public ./public` had nothing to copy)
- Added a `.dockerignore` to all 4 services (`db`, `backend`, `frontend`, `iiot-emitter`) to
  keep build contexts small and avoid leaking local artifacts into images
- Fixed `/fleethub` SignalR disconnects observed under sustained live-ingestion load

---

## v0.1.0 — 2026-06-29

### Add

- **Backend simulation engine** — `TelemetrySimulationService` seeds and simulates 10,000 vehicles
  across 200 synthetic San Francisco street corridors at 500ms tick intervals using `Parallel.ForEach`
- **SignalR hub** (`/fleethub`) — broadcasts `VehicleUpdate[]` batches to all connected clients
  via MessagePack binary protocol for low-latency real-time streaming
- **REST API endpoints:**
  - `GET /api/vehicles` — returns all 10,000 vehicles with current telemetry
  - `GET /api/vehicles/{id}` — returns vehicle detail + last 50 log entries
  - `GET /api/vehicles/{vehicleId}/logs` — returns vehicle event log history
  - `GET /api/vehicles/metadata` — returns static metadata list for all vehicles
- **Vehicle status classification** — deterministic status from metrics:
  offline (fuel=0 or stalled), danger (speed>90 or fuel<10 or temp>85), warning (fuel<30 or temp>70), active (default)
- **Auto-recovery** — offline vehicles recover after 8 seconds with randomized metrics
- **Status rebalancing** — enforces distribution caps every ~20 ticks (offline≤12, danger≤14, warning≤24)
- **Event logging** — per-vehicle log of status changes, overspeed, low fuel, high temp, driver handovers (last 50 entries per vehicle)
- **Next.js 15 dashboard** — real-time fleet monitoring UI with:
  - `<Sidebar>` — virtualized list of 10,000 vehicles with token-based search and status filters
  - `<MapView>` — SF bounding-box map with vehicle position markers colored by status
  - `<DetailPanel>` — 5 circular telemetry gauges (fuel, temp, speed, cargo, engine health) + live logs
  - `<Header>` — notification bell with unread count
  - `<Toast>` — auto-dismiss 2s alert notifications
  - `<NotificationModal>` — persistent notification history with read/unread tracking
- **Zustand state management** — `useFilterStore` (status filters) and `useNotificationStore` (alerts)
- **Alert system** — detects threshold breaches (fuel<20%, temp>65°C, speed>80kph, engineHealth<15%) and fires toast + persistent notifications
- **Docker Compose stack** — `backend` (port 8080) + `frontend` (port 3000) with health checks and `iiot-fleet-net` network
- **Multi-stage Dockerfiles** — backend (SDK 8.0 → ASP.NET 8.0), frontend (Node 18-Alpine)
- **Claude Code configuration** — AGENTS.md roles, sprint template, agent definitions, skills, commands

---

> Sprint 01 (in progress): Swagger UI, Next.js ↔ ASP.NET API wiring, PostgreSQL schema + EF Core integration.
