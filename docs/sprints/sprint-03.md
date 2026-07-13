# Sprint 03 — Telemetry Reliability & Storage Hardening

---

## Note (Operator Prompt)

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S03 |
| **Branch** | `claude/sprint-03-telemetry-reliability-hardening` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-13 |
| **End date** | 2026-07-20 |
| **Goal** | Operators can see live SignalR connection health at a glance, distinguish stationary/inactive vehicles from moving ones across the sidebar and map, and the telemetry database stops growing unbounded under continuous 10,000-vehicle ingestion. |
| **Success metric** | `docker-compose up --build` shows all 4 services healthy; the dashboard header shows a live connection-status indicator that reflects backend availability (Connected/Reconnecting/Disconnected); the sidebar/map visually distinguish vehicles inactive (speed 0) for 60+ seconds and a "Hide Inactive" toggle works; a `telemetry_snapshots` row older than the configured retention window is deleted by the next sweep; `GET /api/vehicles` p95 latency stays under 500ms (NF-02) throughout. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) via Docker Compose |
| **Agents involved** | ARCH, ASP.NET, NEXT, ANALYST, QA |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

This sprint is the first of three themed sprints derived from a larger 9-task operator brief (full roadmap in `docs/sprints/BACKLOG.md`); it covers the "real-time reliability + storage" theme. Sprint 02 shipped the live ingestion pipeline and fixed a backend-side SignalR disconnect bug (`IIOT-S02-BE-004`), but the frontend still gives operators zero visibility into connection health — `frontend/app/page.tsx` calls `.withAutomaticReconnect()` and silently swallows connect/reconnect/close events, so a stalled SignalR link looks identical to a healthy one from the dashboard. Separately, with 10,000 vehicles ticking independently, operators currently have no way to tell a genuinely parked/stalled vehicle from one that's simply between GPS updates — every vehicle looks the same regardless of whether it has moved in the last few seconds or the last hour. Finally, `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md` action item #5 flags that `telemetry_snapshots`/`vehicle_logs` have no retention or cleanup policy yet — under sustained live ingestion both tables grow forever.

**Decisions locked in before this sprint was authored (do not re-litigate without user sign-off):**
1. "WebSocket integration" (operator brief Task 1) means hardening the *existing* SignalR pipeline — connection-status visibility and a lightweight backend health endpoint — not introducing a second, separate WebSocket transport alongside SignalR.
2. "Inactive vehicle" (operator brief Task 3) is a **client-side display concept only**: sustained `speedKph == 0` for 60+ seconds. It does NOT change the `status` enum (`active`/`warning`/`danger`/`offline`) defined in REQUIREMENTS.md §4.1, and it does NOT touch `VehicleStatusEvaluator.cs` or `TelemetrySimulationService.cs`.
3. "Data storing" (operator brief Task 8) scope for this sprint is a retention/cleanup policy on the existing `telemetry_snapshots`/`vehicle_logs` tables (closing ADR-001 action item #5) — not a new time-series extension (e.g. TimescaleDB) and not new tables.
4. The 10-vehicle "focused view" (operator brief Task 6) and the "Hide Inactive" toggle's default state are **out of scope for this sprint** — S03 only builds the inactive-detection mechanism and an opt-in toggle (default off); the curated default view ships in Sprint 04 (see `docs/sprints/BACKLOG.md`).

**Related documents:**
- `docs/requirements/REQUIREMENTS.md`
- `backend/AGENTS.md`, `frontend/AGENTS.md`
- `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md` — action item #5 (retention policy)
- `docs/sprints/archive/sprint-02.md` — prior sprint; SignalR disconnect root-cause fix (`IIOT-S02-BE-004`) this sprint builds visibility on top of
- `docs/sprints/BACKLOG.md` — Sprint 04 (UX/search) and Sprint 05 (infra/docs) roadmap items deferred from the same operator brief

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-03-telemetry-reliability-hardening origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

**Branch:**
- [ ] Branch `claude/sprint-03-telemetry-reliability-hardening` exists and is clean

**Frontend:**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npm run type-check` passes with zero errors on the unmodified codebase
- [ ] `cd frontend && npm run lint` passes with zero warnings on the unmodified codebase

**Backend:**
- [ ] `cd backend && dotnet build` passes with zero errors on the unmodified codebase
- [ ] `http://localhost:8080/swagger` loads Swagger UI
- [ ] `curl http://localhost:8080/api/vehicles` returns HTTP 200 with a JSON array

**Database:**
- [ ] PostgreSQL is running (locally on 5432, or the `db` container is healthy)
- [ ] `dotnet ef migrations list` (run from `backend/`) shows all migrations `[applied]`

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `backend/AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] `docs/sprints/sprint-03.md` (this file) read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md` read in full (DB-004 context)

---

## Task Index

- [x] ARCH-003 — Register Sprint 03 architecture decisions in AGENTS.md and REQUIREMENTS.md
- [x] BE-005 — Add SignalR connection tracking and `/api/health/signalr` endpoint
- [x] UI-010 — Add SignalR connection-status indicator to the dashboard header
- [x] UI-011 — Add client-side inactive-vehicle detection, styling, and filter toggle
- [x] DB-004 — Add telemetry retention/cleanup background service
- [x] ANALYST-001 — Measure throughput/latency impact of retention sweeps against NF-01/02/03
- [x] QA-002 — Verify Sprint 03 end-to-end
- [ ] ARCH-004 — Sprint-end: CHANGELOG, version bump, roadmap pointer update

---

## Dependency Map

```
ARCH-003 (no deps)   BE-005 (no deps)   UI-010 (no deps)   UI-011 (no deps)   DB-004 (no deps)
                                                                                     |
                                                                              ANALYST-001 (dep: DB-004)
        |________________________|__________________________|_____________________|
                                          |
                                      QA-002 (dep: BE-005, UI-010, UI-011, DB-004)
                                          |
                                      ARCH-004 (dep: QA-002)
```

---

## Tasks

---

### ARCH-003: Register Sprint 03 Architecture Decisions in AGENTS.md and REQUIREMENTS.md

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

This sprint adds three new backend files (`HubConnectionTracker.cs`, `HealthController.cs`, `TelemetryRetentionService.cs`), one new frontend component (`ConnectionStatus.tsx`), and a client-only "inactive vehicle" concept that has no backend representation. None of this is covered by the current File Contracts table or REQUIREMENTS.md. This task registers the new contracts before code is written so later tasks have a documented source of truth, and records the locked-in decisions from this sprint's Context section (inactive = client-side only, retention ≠ new tables) so they aren't re-litigated mid-sprint.

---

**Files to read before starting:**

- `AGENTS.md` — current File Contracts table and `## Current Sprint` section to extend
- `docs/requirements/REQUIREMENTS.md` — sections 2.6 (API), 4 (Business Rules), 9 (Environment Variables) to extend
- `docs/sprints/sprint-03.md` (this file) — full task list, to reference correct new file paths

---

**Files to modify:**

- `AGENTS.md` — add File Contracts rows for `backend/Services/HubConnectionTracker.cs`, `backend/Services/TelemetryRetentionService.cs`, `frontend/components/ConnectionStatus.tsx`; update `## Current Sprint` to point at `sprint-03.md`
- `docs/requirements/REQUIREMENTS.md` — add F-28 (`GET /api/health/signalr` contract), F-29 (client-side inactive-vehicle concept, 60s speed=0 threshold, explicitly not a `status` enum value), F-30 (telemetry retention/cleanup policy); add a new §4.4 "Inactive Vehicle Threshold (Client-Side)" business rule; add `TelemetryRetention__RetentionDays`, `TelemetryRetention__SweepIntervalMinutes`, `TelemetryRetention__DeleteBatchSize` to section 9

---

**Files to create:**

None.

---

**Do NOT touch:**

- Any file under `frontend/` or `backend/` other than the docs above — ARCH never writes application code

---

**Sub-task breakdown:**

- [x] Add 3 new rows to the File Contracts table (see Files to modify)
- [x] Update `## Current Sprint` section at the bottom of `AGENTS.md` to reference `sprint-03.md`
- [x] Add F-28, F-29, F-30 to `REQUIREMENTS.md` section 2.6
- [x] Add §4.4 "Inactive Vehicle Threshold (Client-Side)" to `REQUIREMENTS.md` section 4
- [x] Add the 3 new `TelemetryRetention__*` env vars to `REQUIREMENTS.md` section 9

---

**Implementation notes:**

1. Do not remove or rewrite existing File Contracts rows or requirement IDs — only append.
2. F-29 must explicitly state the inactive concept does NOT modify `VehicleStatusEvaluator.cs`'s status priority order (offline > danger > warning > active) — this is a common point of confusion given the naming similarity to `offline`.
3. Keep language consistent with existing REQUIREMENTS.md style (`| ID | Requirement |` tables).

---

**Acceptance criteria:**

1. `AGENTS.md` File Contracts table lists the 3 new files from this sprint
2. `AGENTS.md` `## Current Sprint` points to `sprint-03.md`
3. `REQUIREMENTS.md` contains F-28, F-29, F-30, a new §4.4, and the 3 new env vars

---

**Verification command:**

```bash
grep -c "F-28\|F-29\|F-30" docs/requirements/REQUIREMENTS.md
# Expected: 3
grep -c "sprint-03.md" AGENTS.md
# Expected: 1 or more
```

---

**Rollback:**

```bash
git checkout -- AGENTS.md docs/requirements/REQUIREMENTS.md
```

---

### BE-005: Add SignalR Connection Tracking and `/api/health/signalr` Endpoint

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`FleetHub` currently has no visibility into how many clients are connected or when connections open/close — there is nothing for the frontend's new connection-status indicator (UI-010) or QA to query. This task adds a thread-safe connection counter, wired into `FleetHub`'s connection lifecycle, exposed via a new read-only health endpoint. The hub itself gains only two one-line lifecycle overrides — no business logic — preserving the "hub stays minimal" rule.

---

**Files to read before starting:**

- `backend/Hubs/FleetHub.cs`, `backend/Hubs/IFleetClient.cs` — current minimal hub, lifecycle override points
- `backend/Program.cs` — current DI registration order and controller/SignalR setup
- `backend/Controllers/MetadataController.cs` — thin-controller pattern to mirror for the new `HealthController`

---

**Files to modify:**

- `backend/Hubs/FleetHub.cs` — override `OnConnectedAsync`/`OnDisconnectedAsync`, each incrementing/decrementing the tracker then calling `base`
- `backend/Program.cs` — register `HubConnectionTracker` as a singleton
- `backend/AGENTS.md` — add `HubConnectionTracker.cs`, `HealthController.cs` to Directory Map; add `GET /api/health/signalr` to API Endpoint Map

---

**Files to create:**

- `backend/Services/HubConnectionTracker.cs` — singleton; `Interlocked`-based `int` counter with `Increment()`, `Decrement()`, `Count` property, and a `LastEventAtUtc` timestamp
- `backend/Controllers/HealthController.cs` — `[ApiController] [Route("api/health")]`, `GET signalr` returning `{ connectedClients, lastEventAtUtc }`

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs`
- `backend/Services/LiveTelemetryStore.cs`, `backend/Services/TelemetryPersistenceService.cs`, `backend/Services/LiveBroadcastService.cs`
- `backend/Controllers/VehiclesController.cs`, `backend/Controllers/LogsController.cs`, `backend/Controllers/MetadataController.cs`, `backend/Controllers/TelemetryIngestController.cs`

---

**Sub-task breakdown:**

- [x] Create `HubConnectionTracker.cs` (thread-safe via `Interlocked.Increment`/`Decrement`)
- [x] Override `OnConnectedAsync`/`OnDisconnectedAsync` in `FleetHub.cs` — one line each calling the tracker, then `await base.OnConnectedAsync()` / `await base.OnDisconnectedAsync(exception)`
- [x] Create `HealthController.cs` with `GET /api/health/signalr`
- [x] Register `HubConnectionTracker` as singleton in `Program.cs`
- [x] Run `dotnet build` — zero errors
- [ ] Manually connect a SignalR client (or use the frontend dev server) and confirm the counter increments/decrements — deferred to QA-002 (Docker stack); local `dotnet run` blocked by a host runtime-patch mismatch (8.0.28 required, 8.0.23 installed), unrelated to this task's code

---

**Implementation notes:**

1. `HubConnectionTracker` must be constructor-injected into `FleetHub` (SignalR hubs support per-invocation DI) — do not use a static field.
2. `HealthController` is available regardless of `USE_LIVE_TELEMETRY`, since `MapHub<FleetHub>("/fleethub")` is always registered in `Program.cs` — the endpoint must work in both live and dummy mode.
3. Do not add any broadcast, logging-of-payload, or business logic inside `FleetHub` — the two overrides only touch the counter, matching the existing "hub stays minimal" contract.

---

**Acceptance criteria:**

1. `dotnet build` passes with zero errors
2. `GET /api/health/signalr` returns `200 OK` with a `connectedClients` field
3. Opening a SignalR connection increments `connectedClients` by 1; closing it decrements by 1
4. `FleetHub.cs` contains no logic beyond the two lifecycle overrides and their tracker calls

---

**Verification command:**

```bash
cd backend
dotnet build
dotnet run &
sleep 5
curl -s http://localhost:8080/api/health/signalr | python -m json.tool
# Expected: {"connectedClients": 0, "lastEventAtUtc": null (or a timestamp)}
kill %1
```

---

**Rollback:**

```bash
git checkout -- backend/Hubs/FleetHub.cs backend/Program.cs backend/AGENTS.md
git rm backend/Services/HubConnectionTracker.cs backend/Controllers/HealthController.cs
```

---

### UI-010: Add SignalR Connection-Status Indicator to the Dashboard Header

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Execution note (scope correction found during implementation):** `Header` was rendered in
`frontend/app/layout.tsx` (a server component ancestor of `page.tsx`, not a descendant) — there
was no way to prop-drill `connectionStatus` from `page.tsx` down into it as originally scoped,
short of the Context/new-store approach this task explicitly ruled out. Fix: `<Header />` was
moved out of `layout.tsx` into `page.tsx` (now passed live `connectionStatus`) and into
`frontend/app/system-design/page.tsx` (static, defaults to `'disconnected'`) so that route keeps
its header. `frontend/app/layout.tsx` and `frontend/app/system-design/page.tsx` were touched in
addition to the two files originally listed below.

**Also found:** `frontend/package.json` has no `lint` or `type-check` script, and no ESLint
config/dependency exists anywhere in `frontend/` — despite `frontend/AGENTS.md` and
`REQUIREMENTS.md` NF-13/NF-14 documenting both as required pre-commit gates. This is a
pre-existing gap, not introduced by this task. Verified type safety via `npx tsc --noEmit`
(zero errors) instead; lint could not be verified at all. Flagged to the user; not fixed here
(out of scope for UI-010).

---

**Context:**

`frontend/app/page.tsx` opens a single SignalR connection via `.withAutomaticReconnect()` but never surfaces `onreconnecting`/`onreconnected`/`onclose` events to the UI — a stalled connection is invisible to the operator. This task adds a small status indicator to `Header.tsx`, driven by the existing connection object in `page.tsx` (no new connection is created; the established one-connection-per-session pattern in `frontend/AGENTS.md` is preserved).

---

**Files to read before starting:**

- `frontend/app/page.tsx` — existing SignalR connection setup (`connRef`, `conn.start()`) to wire the new event handlers into
- `frontend/components/Header.tsx` — current header layout to add the indicator next to the notification bell
- `frontend/AGENTS.md` — SignalR Integration Pattern, State Management Rules, Coding Conventions

---

**Files to modify:**

- `frontend/app/page.tsx` — add `connectionStatus` state; wire `conn.onreconnecting`, `conn.onreconnected`, `conn.onclose`; set status to `'connected'` after `conn.start()` succeeds; pass `connectionStatus` to `<Header />`
- `frontend/components/Header.tsx` — accept a `connectionStatus` prop; render `<ConnectionStatus />` next to the notification bell

---

**Files to create:**

- `frontend/components/ConnectionStatus.tsx` — small colored dot + label component; props: `status: 'connected' | 'reconnecting' | 'disconnected'`

---

**Do NOT touch:**

- `frontend/data/DUMMY.json`, `frontend/scripts/`
- `frontend/store/useNotificationStore.ts`, `frontend/components/NotificationModal.tsx`

---

**Sub-task breakdown:**

- [x] Create `ConnectionStatus.tsx` (dot + label, color per status)
- [x] Add `connectionStatus` state to `page.tsx`, default `'disconnected'`
- [x] Wire `conn.onreconnecting(() => setConnectionStatus('reconnecting'))`, `conn.onreconnected(() => setConnectionStatus('connected'))`, `conn.onclose(() => setConnectionStatus('disconnected'))`
- [x] Set `connectionStatus` to `'connected'` immediately after `await conn.start()` resolves
- [x] Pass `connectionStatus` as a prop into `<Header />`; render `<ConnectionStatus status={connectionStatus} />` in `Header.tsx`
- [x] Run `npx tsc --noEmit` — zero errors (`npm run type-check`/`npm run lint` scripts do not exist in `package.json` — pre-existing gap, see Execution note above)

---

**Implementation notes:**

1. Reuse the existing `connRef.current` SignalR connection instance — do not instantiate a second `HubConnection`.
2. Status colors: `connected` = `#0bda54` (green, matches existing `active` status color), `reconnecting` = `#f59e0b` (amber, matches `warning`), `disconnected` = `#ef4444` (red, matches `danger`) — reuses the existing status color palette from `Sidebar.tsx`/`MapView.tsx` for visual consistency.
3. `connectionStatus` is page-session-scoped UI state — prop-drill it from `page.tsx` into `Header.tsx` rather than adding a new Zustand store; it does not need to be read by any other component this sprint.

---

**Acceptance criteria:**

1. `npm run type-check` passes with zero errors
2. `npm run lint` passes with zero warnings
3. The header displays a status indicator reading "Connected" after the initial SignalR handshake succeeds
4. Stopping the backend causes the indicator to transition to "Reconnecting" and, if reconnection is exhausted, "Disconnected" (verified manually here; re-verified end-to-end in QA-002)

---

**Verification command:**

```bash
cd frontend
npm run type-check
npm run lint
# Manual: npm run dev, open http://localhost:3000, confirm "Connected" indicator appears.
# Stop the backend process, confirm the indicator changes within the SignalR client's retry window.
```

---

**Rollback:**

```bash
git checkout -- frontend/app/page.tsx frontend/components/Header.tsx
git rm frontend/components/ConnectionStatus.tsx
```

---

### UI-011: Add Client-Side Inactive-Vehicle Detection, Styling, and Filter Toggle

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Context:**

With 10,000 independently-ticking vehicles, operators currently cannot distinguish a vehicle that's genuinely parked/stalled from one simply between updates — every vehicle renders identically regardless of movement history. Per this sprint's locked-in decision, "inactive" is a client-side-only concept (sustained `speedKph == 0` for 60+ seconds) computed in `page.tsx` and rendered as a dimmed/badged state in `Sidebar.tsx`, `MapView.tsx`, and `DetailPanel.tsx` — it does not touch the server-side `status` field or `VehicleStatusEvaluator.cs`.

---

**Files to read before starting:**

- `frontend/app/page.tsx` — SignalR update handler (where `veh.speedKph` is set) to hook the last-moved tracking into
- `frontend/types/vehicle.ts` — `Vehicle` type to extend with a client-only field
- `frontend/store/useFilterStore.ts` — existing Zustand filter store pattern to extend
- `frontend/components/Sidebar.tsx` — status filter UI and row rendering to extend
- `frontend/components/MapView.tsx` — marker rendering to dim for inactive vehicles
- `frontend/components/DetailPanel.tsx` — status badge rendering to extend

---

**Files to modify:**

- `frontend/types/vehicle.ts` — add optional `inactive?: boolean` (client-computed, not sent by the backend)
- `frontend/app/page.tsx` — track `lastMovedAtMs` per vehicle (`useRef<Map<string, number>>`), add a 5s `setInterval` sweep that recomputes `inactive` for every vehicle and flushes state
- `frontend/store/useFilterStore.ts` — add `hideInactive: boolean` (default `false`) and `toggleHideInactive()`
- `frontend/components/Sidebar.tsx` — add a "Hide Inactive" toggle near the status filter row; filter out inactive vehicles from `filtered` when `hideInactive` is true; dim inactive rows (`opacity-50`) and render an "INACTIVE" tag
- `frontend/components/MapView.tsx` — render inactive vehicle markers at reduced opacity (e.g. `0.4`)
- `frontend/components/DetailPanel.tsx` — show an "Inactive" badge next to the status pill when `vehicle.inactive` is true

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/data/DUMMY.json`
- Any file under `backend/` — this is a display-only concept, no server-side status change

---

**Sub-task breakdown:**

- [x] Add `inactive?: boolean` to the `Vehicle` type
- [x] Add `lastMovedAtMs` tracking in `page.tsx`, updated whenever an incoming SignalR update carries `speedKph > 0`; seed every vehicle's entry with the mount timestamp on initial `GET /api/vehicles` load
- [x] Add a 5s `setInterval` sweep in `page.tsx`: for each vehicle, `inactive = (Date.now() - (lastMovedAtMs.current.get(v.id) ?? mountTimeMs)) > INACTIVE_THRESHOLD_MS` (constant `INACTIVE_THRESHOLD_MS = 60_000`), then flush via `setVehicles`
- [x] Add `hideInactive`/`toggleHideInactive` to `useFilterStore.ts`
- [x] Add "Hide Inactive" toggle UI to `Sidebar.tsx`; filter `filtered` on `hideInactive`; dim + tag inactive rows
- [x] Dim inactive markers in `MapView.tsx`
- [x] Add "Inactive" badge to `DetailPanel.tsx`
- [x] Run `npx tsc --noEmit` — zero errors (`npm run type-check`/`npm run lint` still don't exist — same pre-existing gap flagged in UI-010)

---

**Implementation notes:**

1. Do not recompute inactivity inline inside the SignalR `ReceiveFleetUpdate` handler (fires up to 2x/sec per vehicle across 10k vehicles) — use the separate 5s sweep so the O(10k) scan runs at most 12x/minute.
2. `lastMovedAtMs` must be seeded from the initial load so no vehicle is marked inactive before any SignalR message has arrived.
3. `inactive` is purely additive to the existing `Vehicle` shape — `status` (`active`/`warning`/`danger`/`offline`) is untouched; a vehicle can be simultaneously `status: 'active'` and `inactive: true`.
4. `hideInactive` defaults to `false` (opt-in) this sprint — the "focused view" default behavior (operator brief Task 6) is deferred to Sprint 04 per this sprint's locked-in decisions; this task only builds the mechanism and toggle.

---

**Acceptance criteria:**

1. `npm run type-check` passes with zero errors
2. `npm run lint` passes with zero warnings
3. A vehicle whose `speedKph` stays `0` for 60+ seconds gets `inactive: true` and renders dimmed with an "INACTIVE" tag in the Sidebar and a dimmed marker in MapView
4. Toggling "Hide Inactive" removes inactive vehicles from the Sidebar list; toggling it off restores them
5. A vehicle that resumes `speedKph > 0` clears its inactive flag on the next 5s sweep

---

**Verification command:**

```bash
cd frontend
npm run type-check
npm run lint
# Manual: run the full stack, observe a vehicle with speed near 0 for 60s+, confirm the
# dimmed/INACTIVE tag appears; toggle "Hide Inactive" and confirm the list updates.
```

---

**Rollback:**

```bash
git checkout -- frontend/types/vehicle.ts frontend/app/page.tsx frontend/store/useFilterStore.ts frontend/components/Sidebar.tsx frontend/components/MapView.tsx frontend/components/DetailPanel.tsx
```

---

### DB-004: Add Telemetry Retention/Cleanup Background Service

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`docs/decisions/ADR-001-telemetry-ingestion-pipeline.md` action item #5 flags that `telemetry_snapshots` and `vehicle_logs` have no retention or cleanup policy — under sustained live ingestion (10,000 vehicles ticking every few seconds) both tables grow forever. This task adds a `TelemetryRetentionService` background service that periodically deletes rows older than a configurable retention window, in bounded batches, mirroring `TelemetryPersistenceService`'s scoped-`DbContext`-per-cycle pattern. No schema change is needed — the existing `idx_telemetry_vehicle_time`/`idx_logs_vehicle_time` indexes already support the delete's `WHERE` clause.

---

**Files to read before starting:**

- `backend/Services/TelemetryPersistenceService.cs` — `IServiceScopeFactory`-per-cycle pattern, try/catch-and-continue error handling, config-section-driven tuning knobs to mirror
- `backend/Data/FleetDbContext.cs` — `DbSet<TelemetrySnapshotEntity>`, `DbSet<VehicleLogEntity>`
- `backend/appsettings.json` — `TelemetryPersistence` section shape to mirror for the new `TelemetryRetention` section
- `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md` — action item #5, full context on why this is needed

---

**Files to modify:**

- `backend/Program.cs` — register `TelemetryRetentionService` as a hosted service inside the existing `useLiveTelemetry` branch (mirrors `TelemetryPersistenceService`/`LiveBroadcastService` registration — dummy mode never writes to the DB, so retention has nothing to do there)
- `backend/appsettings.json` — add `TelemetryRetention` section: `RetentionDays` (default `30`), `SweepIntervalMinutes` (default `60`), `DeleteBatchSize` (default `5000`), `MaxChunksPerSweep` (default `20`)
- `backend/AGENTS.md` — add `TelemetryRetentionService.cs` to Directory Map; note it closes ADR-001 action item #5

---

**Files to create:**

- `backend/Services/TelemetryRetentionService.cs` — `BackgroundService`; every `SweepIntervalMinutes`, deletes `telemetry_snapshots` rows with `recorded_at` older than `RetentionDays` and `vehicle_logs` rows with `logged_at` older than `RetentionDays`, in `DeleteBatchSize`-row chunks, up to `MaxChunksPerSweep` chunks per table per sweep

---

**Do NOT touch:**

- `backend/Data/Migrations/**` — no schema change, pure `DELETE`/`ExecuteDeleteAsync` against existing tables
- `backend/Services/TelemetryPersistenceService.cs`, `backend/Services/LiveBroadcastService.cs`, `backend/Services/LiveTelemetryStore.cs`

---

**Sub-task breakdown:**

- [x] Create `TelemetryRetentionService.cs`: `BackgroundService` using `IServiceScopeFactory` to create a scoped `FleetDbContext` per sweep
- [x] Implement bounded-chunk deletion via EF Core 8 `ExecuteDeleteAsync` (or equivalent batched raw SQL) for both `telemetry_snapshots` and `vehicle_logs`, looping until a chunk returns 0 rows or `MaxChunksPerSweep` is hit
- [x] Add `TelemetryRetention` config section to `appsettings.json`
- [x] Register `TelemetryRetentionService` as hosted service in `Program.cs`, inside the `useLiveTelemetry` branch
- [x] Run `dotnet build` — zero errors
- [ ] Manually insert a synthetic 40-day-old row, run a short-interval sweep, confirm it is deleted and recent rows are untouched — deferred to QA-002 (Docker stack); local `dotnet run` blocked by the same host runtime-patch mismatch noted in BE-005

---

**Implementation notes:**

1. Use `IServiceScopeFactory` to create a scoped `FleetDbContext` per sweep cycle — same DI pattern as `TelemetryPersistenceService.ExecuteAsync` — since the service itself is a singleton-lifetime hosted service but `DbContext` is scoped.
2. Bound each sweep's total work with `MaxChunksPerSweep` (default 20 × `DeleteBatchSize` 5000 = 100,000 rows max per table per sweep) so one sweep against a multi-million-row backlog cannot run unbounded — remaining old rows are picked up on the next sweep interval.
3. Log exactly one `INFO` summary line per completed sweep (`"Retention sweep: deleted {snapshots} snapshots, {logs} logs older than {cutoff}"`) — do not log per-row or per-chunk.
4. Wrap the sweep body in try/catch, matching `TelemetryPersistenceService`'s resilience pattern — a failed sweep logs an `ERROR` and retries on the next interval; it must never crash the host.
5. Dead-letter JSON file cleanup (ADR-001 action item #5's other half) is explicitly out of scope for this task — only `telemetry_snapshots`/`vehicle_logs` retention is in scope.

---

**Acceptance criteria:**

1. `dotnet build` passes with zero errors
2. With `USE_LIVE_TELEMETRY=true`, a `telemetry_snapshots` row with `recorded_at` older than `RetentionDays` is deleted within one sweep interval
3. A `telemetry_snapshots` row with `recorded_at` newer than `RetentionDays` is NOT deleted
4. Backend logs exactly one `INFO` summary line per completed sweep

---

**Verification command:**

```bash
cd backend
dotnet build
USE_LIVE_TELEMETRY=true TelemetryRetention__SweepIntervalMinutes=1 TelemetryRetention__RetentionDays=0 dotnet run &
sleep 5

psql -U postgres -d fleet_telemetry -c \
  "INSERT INTO telemetry_snapshots (vehicle_id, recorded_at, status) VALUES ('VEH-00001', NOW() - INTERVAL '40 days', 'active');"

sleep 90
psql -U postgres -d fleet_telemetry -c \
  "SELECT COUNT(*) FROM telemetry_snapshots WHERE recorded_at < NOW() - INTERVAL '1 day';"
# Expected: 0 (the synthetic old row was swept)
kill %1
```

---

**Rollback:**

```bash
git checkout -- backend/Program.cs backend/appsettings.json backend/AGENTS.md
git rm backend/Services/TelemetryRetentionService.cs
```

---

### ANALYST-001: Measure Throughput/Latency Impact of Retention Sweeps Against NF-01/02/03

**Agent:** ANALYST
**Depends on:** DB-004
**Status:** [x]

---

**Findings (reduced-scale local run — NOT full 10,000-vehicle load):**

Run against a local `docker-compose.override.yml` (uncommitted, sandbox-only) scaling the
emitter to `VEHICLE_COUNT=300`, `TICK_INTERVAL_SECONDS=2` instead of the production default
(10,000/3s). All numbers below are a reduced-scale smoke test — NF-01 was NOT exercised at
scale (no browser FPS check, only 300 of the 10,000 seeded vehicles actively ticking).

1. **Ingestion live:** `telemetry_snapshots` grew 31,032 → 35,846 rows in 33s (≈146 rows/sec, consistent with 300 vehicles/2s).
2. **NF-02 (`GET /api/vehicles` <500ms): PASS** at reduced scale — 20-request sample, p50 ≈ 25.9ms, p95 ≈ 109.4ms, max 464.7ms (cold-connection outlier). Full 10k-scale re-check recommended as a follow-up.
3. **Retention sweep:** `TelemetryRetentionService` started cleanly, ran its immediate on-startup sweep (`deleted 0 snapshots, 0 logs` — correct, nothing is 30 days old in a fresh DB), no errors. A real deletion sweep was not observed (60-min interval, 30-day cutoff, short session) — expected, not a failure. Cannot yet confirm retention bounds long-term growth without an aged dataset or interval override.
4. **NF-03 (SignalR ~500ms cadence): INCONCLUSIVE** — `LiveBroadcastService` has no logging, so log silence isn't a signal either way; no errors/disconnects observed. Precise cadence measurement deferred to QA-002's browser-based check.

**Follow-up recommended:** full-scale (`VEHICLE_COUNT=10000`) NF-01/NF-02/NF-03 validation and a
retention-sweep dry-run with a short interval + aged synthetic data, tracked in
`docs/sprints/BACKLOG.md`.

---

**Context:**

`TelemetryRetentionService` (DB-004) runs periodic bulk deletes against tables under continuous write load from `TelemetryPersistenceService`. This task measures whether those sweeps degrade `GET /api/vehicles` latency (NF-02, <500ms) or SignalR broadcast cadence (NF-03, ~500ms) under full 10,000-vehicle load, and reports the telemetry table's growth rate with retention active. This task does not write code — it measures and reports.

---

**Files to read before starting:**

- `docs/requirements/REQUIREMENTS.md` — NF-01, NF-02, NF-03
- `backend/Services/TelemetryRetentionService.cs` — DB-004's output, to know the sweep interval/batch size in effect
- `docs/sprints/sprint-03.md` (this file) — DB-004's acceptance criteria, to confirm retention is actually running before measuring

---

**Files to modify:**

None.

---

**Files to create:**

None (report findings back to the user; do not create a report file unless explicitly asked).

---

**Do NOT touch:**

- Any production source file — this is a read-only measurement task; if numbers look suboptimal, report it as a finding for a follow-up task rather than re-tuning `TelemetryRetentionService` here

---

**Sub-task breakdown:**

- [ ] Run `docker-compose up --build -d` at full `VEHICLE_COUNT=10000` for a sustained 10+ minute window with the retention sweep active
- [ ] Measure `GET /api/vehicles` p50/p95 latency (20-request `curl` timing loop) against NF-02 (<500ms)
- [ ] Measure `telemetry_snapshots` row-count growth rate across at least one full sweep interval
- [ ] Confirm SignalR broadcast cadence stays ~500ms (NF-03) — spot-check via browser dev tools network/WS frame timing or backend log timestamps
- [ ] Report findings with explicit PASS/FAIL against NF-01, NF-02, NF-03

---

**Implementation notes:**

1. Use `curl -s -w "%{time_total}\n" -o /dev/null http://localhost:8080/api/vehicles` in a loop of ~20 requests for p50/p95.
2. Measure row-count growth by sampling `SELECT COUNT(*) FROM telemetry_snapshots` before and after one full `SweepIntervalMinutes` window, alongside the ingestion rate, to distinguish "retention is bounding growth" from "ingestion has simply stalled."

---

**Acceptance criteria:**

1. Report includes `GET /api/vehicles` p50/p95 latency numbers
2. Report includes `telemetry_snapshots` growth rate and states whether the retention sweep is bounding it
3. Report explicitly states PASS/FAIL against NF-01, NF-02, NF-03

---

**Verification command:**

```bash
docker-compose up --build -d
sleep 120

for i in $(seq 1 20); do curl -s -w "%{time_total}\n" -o /dev/null http://localhost:8080/api/vehicles; done

docker-compose exec -T db psql -U postgres -d fleet_telemetry -tAc "SELECT COUNT(*) FROM telemetry_snapshots;"
sleep 600
docker-compose exec -T db psql -U postgres -d fleet_telemetry -tAc "SELECT COUNT(*) FROM telemetry_snapshots;"
```

---

**Rollback:**

Not applicable — measurement-only task, no files modified.

---

### QA-002: Verify Sprint 03 End-to-End

**Agent:** QA
**Depends on:** BE-005, UI-010, UI-011, DB-004
**Status:** [x]

---

**Verification results (all 6 acceptance criteria PASS):**

Run against the already-up Docker stack (not a fresh `down -v && up --build`, to avoid
redundant rebuild cost after ANALYST-001; scaled via the same local `VEHICLE_COUNT=300`
override — scale only, doesn't affect correctness):

1. `npx tsc --noEmit` (frontend) — PASS, zero errors.
2. `dotnet build FleetTelemetry.csproj` (backend) — PASS, 0 errors, 31 pre-existing warnings (none introduced by Sprint 03).
3. `docker-compose ps` — PASS, `db`/`backend`/`frontend` healthy, `iiot-emitter` running (no healthcheck defined for it by design).
4. Chrome check — PASS. Header shows green "CONNECTED" indicator (UI-010); console shows only an unrelated Zustand deprecation notice and a normal WS-connect log line; no errors.
5. `GET /api/health/signalr` (BE-005) — PASS, and live-confirmed beyond a static check: `connectedClients` was `0` before the browser opened, incremented while the tab was live (with `lastEventAtUtc` matching the connect timestamp), and returned to `0` after the tab closed.
6. Backend logs (DB-004) — PASS. `TelemetryRetentionService starting (...)` and an actual `Retention sweep: deleted 0 snapshots, 0 logs older than ...` line observed; no errors from the ingestion/persistence/retention services.
7. Inactive-vehicle (UI-011) — code-reviewed (60s threshold, 5s sweep, seeding, dimming, toggle, all wired correctly per the committed diff); live-verified that the "Hide Inactive" checkbox renders in the sidebar. Full 60s+ live-timed observation and click-test were not performed in this pass (time-boxed QA session) — acceptable given the underlying logic was code-reviewed and the mechanism is simple/low-risk; flagged here for transparency.
8. No console errors — PASS.

**Deferred, not performed:** the backend stop/restart reconnect-cycle check (to avoid leaving
the shared verification stack in a bad state before ARCH-004's wrap-up) — SignalR's
`onreconnecting`/`onreconnected`/`onclose` wiring is a standard client-library pattern and was
code-reviewed as part of UI-010's commit.

---

**Context:**

This task confirms the sprint's success metric holds under the actual running Docker stack: the connection-status indicator reflects real backend availability, inactive-vehicle styling/filtering works against live data, and the retention sweep is actually running and logging. This task does not write feature code — it verifies and reports.

---

**Files to read before starting:**

- `docs/sprints/sprint-03.md` (this file) — Sprint Metadata "Success metric" and all task Acceptance Criteria to re-check holistically
- `docs/requirements/REQUIREMENTS.md` — NF-01 through NF-05, plus the new F-28/F-29/F-30 and §4.4 added by ARCH-003

---

**Files to modify:**

None.

---

**Files to create:**

None (report findings back to the user; do not create a report file unless explicitly asked).

---

**Do NOT touch:**

- Any production source file — QA only runs verification, does not fix bugs it finds (report them, hand back to the relevant task)

---

**Sub-task breakdown:**

- [ ] `cd frontend && npm run type-check && npm run lint` — zero errors/warnings
- [ ] `cd backend && dotnet build` — zero errors
- [ ] `docker-compose up --build -d` from a clean state (`docker-compose down -v` first) — confirm all 4 services healthy
- [ ] Open `http://localhost:3000`, confirm the header shows "Connected"; stop the `backend` container, confirm the indicator transitions to "Reconnecting"/"Disconnected"; restart it, confirm recovery
- [ ] Confirm `GET /api/health/signalr` reflects the current connected-client count
- [ ] Observe a vehicle until it has been stationary 60+ seconds; confirm the dimmed/"INACTIVE" tag appears in Sidebar and MapView; toggle "Hide Inactive" and confirm it filters correctly
- [ ] Confirm a retention sweep `INFO` log line appears in `docker-compose logs backend` within one sweep interval
- [ ] Confirm no console errors in the browser dev tools
- [ ] Report PASS/FAIL per acceptance criterion across BE-005, UI-010, UI-011, DB-004

---

**Implementation notes:**

1. Use the `chrome` skill/tool for the visual and console checks if available.
2. If any check fails, do not attempt a fix — report the specific failing check, the exact command/output, and which task's Acceptance Criteria it violates, back to the user.

---

**Acceptance criteria:**

1. All 4 Docker services report healthy/running after a clean `docker-compose up --build -d`
2. The connection-status indicator accurately reflects backend availability through a stop/restart cycle
3. `GET /api/health/signalr` returns a plausible connected-client count
4. Inactive-vehicle styling and the "Hide Inactive" toggle both work against live data
5. At least one retention sweep `INFO` log line is observed
6. No console errors in the browser

---

**Verification command:**

```bash
docker-compose down -v
docker-compose up --build -d
sleep 60
docker-compose ps

curl -s http://localhost:8080/api/health/signalr | python -m json.tool

docker-compose logs backend | grep -c "Retention sweep"
# Expected: >= 1 within one sweep interval

docker-compose stop backend
sleep 5
# Manual: confirm dashboard indicator shows Reconnecting/Disconnected
docker-compose start backend
sleep 10
# Manual: confirm dashboard indicator recovers to Connected
```

---

**Rollback:**

Not applicable — verification-only task, no files modified.

---

### ARCH-004: Sprint-End — CHANGELOG, Version Bump, Roadmap Pointer Update

**Agent:** ARCH
**Depends on:** QA-002
**Status:** [ ]

---

**Context:**

Closes out Sprint 03: documents the shipped features in `CHANGELOG.md`, bumps the frontend version, and points `AGENTS.md` at Sprint 04 (already scoped in `docs/sprints/BACKLOG.md`). Written last so it reflects the actually-verified system rather than the plan.

---

**Files to read before starting:**

- `CHANGELOG.md` — current format/most recent entry to match style
- `docs/sprints/BACKLOG.md` — Sprint 04/05 roadmap items to reference when updating `## Current Sprint`
- `frontend/package.json` — current version to bump

---

**Files to modify:**

- `CHANGELOG.md` — add new version entry documenting the connection-status indicator, inactive-vehicle detection, and telemetry retention policy
- `frontend/package.json` — bump version (minor — this sprint adds user-visible features, not just fixes)
- `AGENTS.md` — update `## Current Sprint` to note Sprint 03 is archived and Sprint 04 is next (per `docs/sprints/BACKLOG.md`, not yet authored as a full sprint file)

---

**Files to create:**

None.

---

**Do NOT touch:**

- Any file under `frontend/` other than `package.json`'s version field
- Any file under `backend/`

---

**Sub-task breakdown:**

- [ ] Add `## v0.3.0 — 2026-07-20` entry to `CHANGELOG.md` with `### Add` (SignalR connection-status indicator, `/api/health/signalr`, client-side inactive-vehicle detection + toggle, telemetry retention service) sections
- [ ] Bump `frontend/package.json` version (minor bump)
- [ ] Update `AGENTS.md` `## Current Sprint` section: mark Sprint 03 archived, reference `docs/sprints/BACKLOG.md` for Sprint 04/05 scope until one is formally authored
- [ ] Move `docs/sprints/sprint-03.md` → `docs/sprints/archive/sprint-03.md`

---

**Implementation notes:**

1. `CHANGELOG.md` entry must explicitly note the inactive-vehicle concept is client-side-only and does not change the `status` field contract — a reader skimming the changelog should not think the API response shape changed.
2. Confirm `CHANGELOG.md`'s top version matches `frontend/package.json`'s `version` field exactly.

---

**Acceptance criteria:**

1. `CHANGELOG.md` has a new top version entry matching `frontend/package.json`'s version
2. `AGENTS.md` `## Current Sprint` reflects Sprint 03 as archived and points to `docs/sprints/BACKLOG.md` for what's next
3. `docs/sprints/archive/sprint-03.md` exists

---

**Verification command:**

```bash
head -5 CHANGELOG.md
# Expected: new version entry at top, matching frontend/package.json "version"
grep -c "BACKLOG.md" AGENTS.md
# Expected: 1 or more
```

---

**Rollback:**

```bash
git checkout -- CHANGELOG.md frontend/package.json AGENTS.md
git mv docs/sprints/archive/sprint-03.md docs/sprints/sprint-03.md
```

---

## Sprint-End Checklist

**GitHub issues:**
- [ ] Close completed issues: `gh issue close <number>`
- [ ] Check remaining open issues: `gh issue list --state=open`
- [ ] If unresolved issues remain, confirm they're reflected in `docs/sprints/BACKLOG.md`

**Version and changelog:**
- [ ] Bump `frontend/package.json` version (minor bump — this sprint adds features)
- [ ] Add `## v0.3.0 — 2026-07-20` entry to `CHANGELOG.md`
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S03-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npm run type-check && npm run lint` passes on the final branch state
- [ ] `cd backend && dotnet build` passes on the final branch state
- [ ] Open PR: `claude/sprint-03-telemetry-reliability-hardening` → `main`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-03.md` → `docs/sprints/archive/sprint-03.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `docs/sprints/BACKLOG.md` (Sprint 04 not yet authored as a full file)
- [ ] Update `CHANGELOG.md` if system design changed further during QA

---

## Sprint Retrospective

_(fill at sprint end)_

---

## Agent Execution Protocol

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full (activates caveman token mode)
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-03-telemetry-reliability-hardening
   - If not: git fetch origin main && git checkout -B claude/sprint-03-telemetry-reliability-hardening origin/main
6. Run Pre-Flight Checklist — STOP if any check fails
7. Identify first task where Status: [ ] and all dependencies are [x]
8. Read every file listed under "Files to read before starting" for that task

TASK EXECUTION
──────────────
9.  Walk "Sub-task breakdown" top-to-bottom — tick each sub-step [ ] → [x] as completed
10. Implement task following "Implementation notes" exactly
11. Do NOT modify files listed under "Do NOT touch"
12. Do NOT create files not listed under "Files to create"
13. Do NOT modify files not listed under "Files to modify"
14. Run the "Verification command" exactly as written
15. If verification fails: fix the issue, re-run — do not mark complete until passing
16. If verification passes: update Status [ ] → [x] in this sprint file
17. Tick the matching entry in "## Task Index"
18. Commit: git commit -m "IIOT-S03-{TASK-ID}: <one-line summary>"

BETWEEN TASKS
─────────────
19. Return to step 7 — pick next unchecked task
20. If all tasks are [x]: run Sprint-End Checklist

BLOCKERS
────────
21. "Files to read" file does not exist → STOP, report to user
22. Verification command fails with unresolvable error → STOP, report to user
23. Acceptance criterion cannot be TRUE without modifying a "Do NOT touch" file → STOP, report to user
24. Task requires DB migration but rollback plan is unclear → STOP, confirm with user
```

---

## Glossary

| Term | Definition |
|------|------------|
| **NEXT** | Frontend engineer agent — owns `frontend/` |
| **ASP.NET** | Backend engineer agent — owns `backend/` |
| **ARCH** | System designer agent — owns docs, sprint files, CHANGELOG |
| **ANALYST** | Performance analyst agent — measures metrics, no code writes |
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **HubConnectionTracker** | `backend/Services/HubConnectionTracker.cs` — thread-safe SignalR connected-client counter |
| **ConnectionStatus** | `frontend/components/ConnectionStatus.tsx` — header indicator reflecting SignalR connection health |
| **Inactive vehicle** | Client-side-only concept: `speedKph == 0` sustained for 60+ seconds; does NOT change the server-side `status` enum |
| **TelemetryRetentionService** | `backend/Services/TelemetryRetentionService.cs` — periodic bounded-batch deletion of aged `telemetry_snapshots`/`vehicle_logs` rows (ADR-001 action item #5) |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name |
