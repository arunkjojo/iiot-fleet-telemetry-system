# Changelog

All notable changes to the IIoT Fleet Telemetry System are documented here.

Format: `## vX.Y.Z — YYYY-MM-DD` with sections `### Add`, `### Fix`, `### Update`.
Version is bumped once per sprint at sprint-end by the ARCH agent.

---

## v0.8.2 — 2026-07-22

### Fix

- **Worldwide fleet distribution** (`IIOT-S09-EMIT-002`): `emitter/emitter.py` replaced the
  San-Francisco-only waypoint list with ~35 real-city land anchors spanning every inhabited
  continent (`WORLD_LAND_ANCHORS`). Each vehicle is assigned one fixed home anchor for its
  lifetime and roams within ~6-7km of it (`local_destination`, longitude jitter scaled by
  `1/cos(latitude)`), so the fleet spreads across the globe while never generating a
  straight-line "drive" across open ocean between two continents. `docs/requirements/REQUIREMENTS.md`
  §5.1/§10 updated to describe worldwide GPS ranges instead of the SF bbox.

---

## v0.8.1 — 2026-07-22

### Fix

- **Land-constrained emitter positions**: `emitter/emitter.py` replaced raw bounding-box
  `random.uniform` position sampling (which regularly placed vehicles in San Francisco
  Bay/ocean water) with a curated 35-point on-land waypoint list and waypoint-to-waypoint
  destination-seeking motion. Owned going forward by the new EMIT agent/skill
  (`.claude/agents/iiot-emitter.md`, `.claude/skills/iiot-emitter/SKILL.md`).
- **Marker clustering**: `frontend/components/MapView.tsx` wraps vehicle markers in
  `@changey/react-leaflet-markercluster`'s `MarkerClusterGroup`, addressing dashboard lag
  at full fleet scale (previously one raw Leaflet `<Marker>` DOM node per vehicle).
- Root `.gitignore` now excludes `__pycache__/`/`*.pyc`.

---

## v0.8.0 — 2026-07-22

### Add

- **Interactive Leaflet map**: `frontend/components/MapView.tsx` now renders vehicles on a
  real `react-leaflet` map (OpenStreetMap tiles) at their true lat/lng, replacing the prior
  static background-image projection. Markers use custom status-color `divIcon`s; the map
  fits its initial view to the current vehicle bounding box.
- **Swagger UI**: the backend now serves interactive OpenAPI docs at `/swagger` in local dev,
  the Docker Compose stack, and Helm-deployed pods (`Swashbuckle.AspNetCore`, ungated by
  `ASPNETCORE_ENVIRONMENT`).
- **Data-flow diagram**: `docs/APPLICATION_OVERVIEW.md` gained a Mermaid flowchart covering
  the emitter → ingest → persistence/broadcast → SignalR → frontend write path, and the
  separate REST read path.

### Update

- **Live-only backend**: removed `TelemetrySimulationService` and the `USE_LIVE_TELEMETRY`
  toggle entirely — the backend now always sources vehicle state from live emitter ingestion.
  Compose/Helm/docs updated to match.
- `docs/requirements/REQUIREMENTS.md` F-26/F-27/F-32/§5.1/§9 updated to describe the
  live-only pipeline; doc version bumped 0.1 → 0.2.

### Fix

- `frontend/app/page.tsx` wraps the `MapView` import in `next/dynamic({ ssr: false })` —
  Leaflet touches `window` at import time, which previously broke `next build`'s prerender.

---

## v0.7.1 — 2026-07-17

### Update

- **Docker/Helm infra restructured** (operator addendum to Sprint 07, same branch/PR):
  - Dockerfiles moved to `containers/<service>/Dockerfile` (`backend`, `frontend`, `emitter`);
    `containers/docker-compose.yml`'s `build.context` repointed at the real source dirs
    (`../backend`, `../frontend`, `../emitter`); `.dockerignore` files moved back into those
    source dirs to match Docker's context-root-relative lookup.
  - `db` no longer builds a custom image — `db/Dockerfile` and `db/postgresql.conf` are removed;
    both `containers/docker-compose.yml` and the Helm chart now pull `postgres:16-alpine`
    directly.
  - `iiot-emitter` renamed to `emitter` throughout the codebase, Helm chart, and active docs
    (directory, Compose service name, Helm values/templates, env-var docs). Archived sprint
    files and the existing ADR are left as historical record, unchanged.
  - `DOCKER_README.md` moved to `docs/DOCKER_README.md`; every link across `README.md`,
    `AGENTS.md`, `docs/HELM_GUIDE.md`, `docs/PROJECT_OVERVIEW.md`, and
    `docs/devops-learn/Docker_Compose.md` updated to match.
  - `.github/workflows/docker-image.yml` removed — this project runs no CI pipeline.
- **Helm chart templates reorganized into per-service folders** — `templates/{backend,frontend,
  db,emitter}/` each hold that service's `Deployment`/`Service`/`Secret`, with inline comments
  explaining why each file exists and why it's shaped the way it is. `_helpers.tpl`,
  `app-configmap.yaml`, `ingress.yaml`, and `NOTES.txt` stay at the templates root since they're
  genuinely cross-cutting, not service-specific (each now documents why in a header comment).
  `NOTES.txt` also gained a comment explaining what it actually is: Helm-rendered CLI text shown
  after `helm install`/`upgrade`, never applied to the cluster and never written to disk.
  Chart version bumped `0.1.0` → `0.2.0` for the structural change; `appVersion` synced to
  `0.7.1`.

---

## v0.7.0 — 2026-07-17

### Add

- **`docs/APPLICATION_OVERVIEW.md`** — explains what the system is, its frontend/backend/db/emitter
  topology, and both data-flow paths (dummy-mode simulation loop, live-mode ingestion →
  persistence → broadcast), including the `USE_LIVE_TELEMETRY` branch point. Linked from
  `README.md` and `AGENTS.md`.
- **`docs/devops-learn/`** — three new onboarding guides distinct from the existing operational
  docs (`DOCKER_README.md`, `docs/HELM_GUIDE.md`): `Docker_Compose.md`, `Helm.md`, and `K8s.md`,
  each covering core concepts first and then mapping them onto this repo's actual
  `docker-compose.yml` / `helm/iiot-fleet-app/` chart.
- **`REQUIREMENTS.md` `F-35`** — the sidebar's "always show the full filtered list, no inactivity
  hiding, no top-N cap" behavior, replacing the removed `F-33`/`F-34`.

### Fix

- Rebalancer caps in `TelemetrySimulationService` no longer sit at three fixed constants
  (offline 12 / danger 14 / warning 24) that under-represented `danger`/`warning` at fleet scale —
  each rebalance tick now re-rolls a random target within a wider range (offline 40–100, danger
  100–400, warning 500–800), giving a more realistic, drifting distribution.

### Update

- **Vehicle status thresholds** — `VehicleStatusEvaluator.Evaluate` (live mode) and
  `TelemetrySimulationService.EvaluateStatus` (dummy mode) both now implement new
  offline/danger/warning/active threshold rules (see `REQUIREMENTS.md` §4.1); the `active` band's
  fuel condition is a closed range, `30.0 <= fuelPercent <= 100.0`.
- **Sidebar simplified** — the "Hide Inactive" checkbox and "Focused View (Top 10)" toggle are
  removed from `frontend/components/Sidebar.tsx` and `frontend/store/useFilterStore.ts`; the
  sidebar always lists the full search/status-filtered result set.
- **Client-side "inactive vehicle" concept removed entirely** — the 60s-sustained-zero-speed sweep
  in `frontend/app/page.tsx`, the dimming in `MapView.tsx`, the badge in `DetailPanel.tsx`, and the
  `inactive`/`lastSeenAtUtc` fields on the frontend `Vehicle` type are all gone, since nothing in
  `REQUIREMENTS.md` backs the concept anymore (`F-29`, `F-33`, `F-34`, and §4.4/`BR-01`–`BR-04`
  are all removed/superseded by `F-35`).
- **`REQUIREMENTS.md`** §4.1 and §4.2 rewritten to match the new thresholds/caps; §4.4 deleted.

---

## v0.6.0 — 2026-07-21

### Add

- **`docs/SDD_WORKFLOW.md`** — documents the Spec-Driven Development loop this project already
  follows informally: requirements (`docs/requirements/REQUIREMENTS.md`) → sprint authoring (the
  `sprint` skill) → task execution (the per-task, per-commit Sprint Loop) → verification (each
  task's own acceptance criteria plus a sprint-end QA task) → changelog/archive. Linked from
  `README.md` and `AGENTS.md`'s Key Knowledge Base Documents table. Registers `helm/**` as an
  INFRA write-scope path ahead of the Helm chart work below.
- **Explicit Docker Compose network** — `docker-compose.yml`'s `db`/`backend`/`frontend`/
  `iiot-emitter` services now join a top-level, explicitly declared `iiot-fleet-net` bridge
  network (`driver: bridge`), replacing the old `networks.default.name` shorthand. Purely
  additive — no port, volume, or service-name change; `postgres_data` remains the sole named
  volume.
- **Helm chart (`helm/iiot-fleet-app/`)** — a full chart deploying all four services to
  Kubernetes: a `db` `StatefulSet` with `volumeClaimTemplates`-backed persistent storage and a
  headless `Service`; `backend`/`frontend` `Deployment`s + `ClusterIP` `Service`s (`tcpSocket`
  and `httpGet` readiness/liveness probes respectively, matching each service's actual Compose
  healthcheck behavior); a shared `ConfigMap` for non-secret env vars and a dedicated `Secret`
  assembling the backend's `ConnectionStrings__Fleet` so the db password never lands in the
  `ConfigMap`; an `emitter` `Deployment` (no matching `Service` — outbound-only, fixed at
  `replicaCount: 1` since the emitter simulates the *entire* fleet per replica) gated on an
  `initContainer` that polls the backend before starting, approximating Compose's
  `depends_on: condition: service_healthy` (Kubernetes has no direct equivalent); and an
  optional `Ingress`, off by default (`ingress.enabled: false`), routing `/api`/`/swagger`/
  `/fleethub` to the backend and `/` to the frontend when enabled.
- **`docs/HELM_GUIDE.md`** — install/configuration/troubleshooting guide for the Helm chart:
  prerequisites, building and loading the four unpublished custom images, a full `values.yaml`
  reference table, `helm upgrade`/`helm uninstall` (including PVC-retention behavior), `kubectl
  port-forward` connection examples, a worked `kind`-cluster end-to-end walkthrough, and
  troubleshooting for `ImagePullBackOff`, a `Pending` PVC, the emitter's init-gate delay, and an
  `Ingress` with no controller installed. Linked from `README.md` and `AGENTS.md`.

---

## v0.5.0 — 2026-07-16

### Add

- **`docs/PROJECT_OVERVIEW.md`** — a comprehensive onboarding document covering application
  architecture (all 4 services + data flow), the use case/problem the system solves, key design
  decisions (linking `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md`), the DevOps stack
  (Docker/Compose/GitHub Actions, linking `DOCKER_README.md`), the AI-assisted development
  workflow (the actual ARCH/ASP.NET/NEXT/INFRA/QA/ANALYST agents, `sprint`/`devops`/`nextjs`/
  `asp-dot-net-core`/`postgre-sql`/`caveman` skills, and the branch-per-sprint,
  task-by-task-with-per-task-commits sprint loop used to build this codebase), a factual
  Sprint 01-04 project history, and a "Getting Started" section — linked from `README.MD`.
  Written as a map (short sections, links to authoritative sources) rather than a duplicate of
  `AGENTS.md`/`REQUIREMENTS.md`/`DOCKER_README.md`, so it doesn't drift out of sync with them.
  Closes Task 9 of the 2026-07-13 operator brief and, with it, the full 9-task/3-sprint arc
  (Sprints 03-05 plus the standalone Task 7 CI fix) — see `docs/sprints/BACKLOG.md` for the
  brief's final delivery summary and remaining open carryover items. Verified by QA-005: all
  internal links resolve, no placeholder text, and content is factually consistent with
  `AGENTS.md`/`REQUIREMENTS.md`/the archived sprint files.

---

## v0.4.0 — 2026-07-20

### Add

- **Editable vehicle driver name / fleet number** — `PATCH /api/vehicles/{id}` accepts an
  optional `driverName` and/or `displayNumber` and updates only those fields; the `id` path
  parameter (primary key, FK target for `telemetry_snapshots`/`vehicle_logs`, and the exact
  string the Python emitter sources from `GET /api/vehicles/metadata`) is never renamed or
  accepted as a mutable field. Backed by a new nullable `vehicles.display_number VARCHAR(30)`
  column (seeded as `FL-NNNNN` for fresh rows) and surfaced via an inline edit affordance in
  `DetailPanel.tsx` (driver name / display number fields, client-side validated, optimistic
  local-state update on success, no page reload required)
- **24h-activity search filter** — when the Sidebar search box has a non-empty query, results
  additionally exclude vehicles with no telemetry activity (`lastSeenAtUtc`) in the last 24
  hours; does not apply to the unfiltered/status-filtered list. Backed by a new
  `lastSeenAtUtc` field exposed per vehicle on `GET /api/vehicles`/`GET /api/vehicles/{id}`
  (live mode: last ingest time via `ILiveTelemetryStore`; dummy mode: always "now")
- **Default-on focused view** — the Sidebar now defaults to showing at most 10 curated
  (highest-priority, status-sorted) vehicles, with a "Show all" toggle to reveal the full
  virtualized list; the 10-item cap does not apply while a search query is active. Builds on
  Sprint 03's `hideInactive` mechanism in `useFilterStore.ts`

### Fix

- **Dummy-mode vehicle IDs are now meaningful** — `TelemetrySimulationService.MakeId()`
  previously generated random 2-3-letter-prefix + 4-6-character-suffix gibberish IDs (e.g.
  `"XJ-4K7Q2"`) for local `dotnet run` / dummy-mode seeding; it now generates `VEH-NNNNN`
  (zero-padded 5-digit) IDs deterministically from the seeding loop index, matching the format
  already used by live mode, the DB-seeded `vehicles` table, and the Python emitter
- **PATCH edits no longer clobbered by the next live-ingestion tick** — found by QA-003's
  first verification pass (mirroring Sprint 02's QA-001/BE-004 precedent): a successful
  `PATCH /api/vehicles/{id}` reverted within one ingest tick under live/Docker conditions,
  because `TelemetryIngestController`'s `Ingest` action rebuilt a fresh `Vehicle` object on
  every tick without preserving an edited `DriverName`/`DisplayNumber`. Fixed by flipping
  field priority so the live store's existing (potentially PATCH-edited) state wins over the
  incoming ingest request for those two fields specifically; re-verified holding across
  multiple ingest ticks while other telemetry fields continue updating normally

### Update

- **Responsive/overflow fixes** — `Header`, `Sidebar`, `MapView`, and `DetailPanel` no longer
  clip content or force horizontal page scroll at mobile (375px) or tablet (768px) viewport
  widths; `Sidebar`/`DetailPanel` become full-width overlays below the `md:` breakpoint instead
  of fixed-width side panels, `Header` wraps/truncates gracefully, and a second nested overflow
  inside `DetailPanel`'s gauge/driver-model grids was fixed along the way. Desktop (≥1024px)
  layout is unchanged

### Known Issues

- `ILiveTelemetryStore` is never hydrated from Postgres's DB-seeded `display_number` on
  backend startup — a freshly-started live-mode backend shows `displayNumber: ""` for every
  vehicle until an operator PATCHes one in, rather than the DB-seeded `FL-NNNNN` default.
  Intentionally out of scope for this sprint's BE-009 clobbering fix (a data-loss bug, not a
  cold-start-hydration gap). Tracked in `docs/sprints/BACKLOG.md`.
- QA-003 verified dummy-mode `VEH-NNNNN` ID format and the 24h search-exclusion rule via code
  review rather than a live end-to-end run, due to this sandbox's known host .NET
  runtime-patch mismatch (blocking local `dotnet run`) and the inability to synthesize a
  live stale (>24h) test vehicle within QA's read-only mandate, respectively. Both carried
  forward from prior sprints' documented sandbox limitations.

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
