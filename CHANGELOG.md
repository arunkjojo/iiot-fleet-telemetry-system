# Changelog

All notable changes to the IIoT Fleet Telemetry System are documented here.

Format: `## vX.Y.Z — YYYY-MM-DD` with sections `### Add`, `### Fix`, `### Update`.
Version is bumped once per sprint at sprint-end by the ARCH agent.

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
