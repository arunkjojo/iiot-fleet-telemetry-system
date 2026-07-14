# Sprint 04 — Vehicle Editing, Recency Search, and Focused View

---

## Note (Operator Prompt)

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S04 |
| **Branch** | `claude/sprint-04-editing-search-focused-view` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-13 |
| **End date** | 2026-07-20 |
| **Goal** | Operators can rename a vehicle's fleet number and driver from the dashboard, dummy/local-dev mode no longer shows randomly-generated gibberish vehicle IDs, search results are bounded to vehicles active in the last 24 hours, the sidebar defaults to a curated top-10 view instead of always rendering the full 10,000-vehicle list, and the UI no longer clips/overflows at common mobile/tablet breakpoints. |
| **Success metric** | `PATCH /api/vehicles/{id}` persists a new driver name / display number and the dashboard reflects it without a page reload; `dotnet run` (dummy mode) seeds vehicles with `VEH-NNNNN`-style IDs instead of random strings; searching the sidebar excludes vehicles with no telemetry in the last 24h; the sidebar shows ≤10 vehicles by default with a working "Show all" toggle; `Sidebar`/`MapView`/`DetailPanel`/`Header` render without horizontal overflow or clipped content at 375px and 768px viewport widths. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) via Docker Compose |
| **Agents involved** | ARCH, ASP.NET, NEXT, QA |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

Second of three themed sprints split from the 2026-07-13 operator brief (see `docs/sprints/BACKLOG.md` for the full roadmap; Sprint 03 shipped SignalR connection-status visibility, inactive-vehicle detection, and telemetry retention). This sprint covers Tasks 2, 4, 5, 6 from that brief. While grounding Task 2's scope against the current codebase, a second, more fundamental problem was found beyond "no editable fields exist" (which the operator already confirmed as the primary ask): `backend/Services/TelemetrySimulationService.cs`'s `MakeId()` (lines ~190-204) generates **random 2-3 letter + 4-6 alphanumeric-character IDs** (e.g. `"XJ-4K7Q2"`) for dummy/local-dev mode, completely unrelated to the `VEH-00001`..`VEH-09999` format used everywhere else (live mode, the DB-seeded `vehicles` table, the Python emitter). This directly matches the operator's original complaint that vehicle numbers aren't "meaningful" — in dummy mode they are literally random garbage. This sprint fixes both: the ID-generation bug (dummy mode) and adds the editable-fields feature (both modes, backed by a new `vehicles.display_number` column).

**Decisions locked in before this sprint was authored (do not re-litigate without user sign-off):**
1. "Editable vehicle number" means a **new `display_number` column** on the `vehicles` table (e.g. seeded as `FL-00001`-style), edited via a new `PATCH /api/vehicles/{id}` endpoint — the existing `id` (`VEH-00001`, primary key, foreign-key target for `telemetry_snapshots`/`vehicle_logs`, and the exact string the Python emitter must use per `GET /api/vehicles/metadata`) is **never** renamed. Renaming the PK would break FK integrity and the emitter's roster contract.
2. `PATCH /api/vehicles/{id}` updates the Postgres `vehicles` row AND the currently-active in-memory store (`ILiveTelemetryStore` in live mode, `TelemetrySimulationService.Vehicles` in dummy mode) so an edit is visible immediately without a service restart — but this does **not** require adding DB calls to `TelemetrySimulationService.cs` itself; the controller mutates the already-`internal`ly-exposed static `Vehicles` dictionary directly (the same pattern `VehiclesController`/`LogsController` already use to *read* it since BE-003).
3. "Search ... date-time based (last 24 hours)" means: when the sidebar search box has a non-empty query, results are additionally filtered to vehicles with a `lastSeenAtUtc` within the last 24 hours. It is not a separate date-range picker.
4. The 10-vehicle "focused view" (operator brief Task 6) defaults **on**; a "Show all 10,000" toggle reveals the full virtualized list. This does not violate NF-01 (10k-vehicle render) since the full list remains one click away and is still fully virtualized when shown.
5. Task 4 ("UI modifications... avoid the screen side point issues") is interpreted, absent a screenshot from the operator, as: a responsive/overflow audit and fix across the 4 primary layout components (`Header`, `Sidebar`, `MapView`, `DetailPanel`) at mobile (375px) and tablet (768px) viewport widths — no horizontal scroll, no clipped content at screen edges, consistent spacing. If this doesn't match what the operator meant, flag it after this sprint for a follow-up with concrete examples.

**Related documents:**
- `docs/requirements/REQUIREMENTS.md`
- `backend/AGENTS.md`, `frontend/AGENTS.md`
- `docs/sprints/archive/sprint-03.md` — prior sprint; this sprint builds the "focused view" on top of its `inactive`/`hideInactive` mechanism (`UI-011`)
- `docs/sprints/BACKLOG.md` — full 9-task operator brief and 3-sprint split

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-04-editing-search-focused-view origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

**Branch:**
- [ ] Branch `claude/sprint-04-editing-search-focused-view` exists and is clean

**Frontend:**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npx tsc --noEmit` passes with zero errors on the unmodified codebase (note: `npm run type-check`/`npm run lint` scripts do not exist in `package.json` — a pre-existing gap tracked in `docs/sprints/BACKLOG.md`; use `npx tsc --noEmit` directly)

**Backend:**
- [ ] `cd backend && dotnet build FleetTelemetry.csproj` passes with zero errors on the unmodified codebase (plain `dotnet build` fails on a pre-existing unrelated `.sln` path issue — use the explicit csproj path)
- [ ] `http://localhost:8080/swagger` loads Swagger UI (when running)

**Database:**
- [ ] PostgreSQL is running (locally on 5432, or the `db` container is healthy)
- [ ] `dotnet ef migrations list` (run from `backend/`) shows all migrations `[applied]`

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `backend/AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] `docs/sprints/sprint-04.md` (this file) read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] `backend/Services/TelemetrySimulationService.cs` lines ~186-235 read (`MakeId()`, vehicle seeding) — BE-008's exact target

---

## Task Index

- [x] ARCH-005 — Register Sprint 04 architecture decisions in AGENTS.md and REQUIREMENTS.md
- [x] BE-008 — Fix dummy-mode vehicle ID generation to be meaningful/consistent
- [x] DB-005 — Add `display_number` column to `vehicles` (migration + entity + seeder)
- [x] BE-006 — Add `PATCH /api/vehicles/{id}` endpoint (driver name + display number)
- [x] UI-012 — Add vehicle edit UI to DetailPanel
- [x] BE-007 — Expose `lastSeenAtUtc` per vehicle
- [x] UI-013 — Apply 24h-activity filter to sidebar search
- [x] UI-014 — Add default-on focused view (max 10 vehicles + "Show all" toggle)
- [x] UI-015 — Responsive/overflow audit and fix
- [x] BE-009 — Fix PATCH edits clobbered by the next ingest tick (QA-003 finding, ad hoc — not in the original 11-task plan)
- [x] QA-003 — Verify Sprint 04 end-to-end
- [ ] ARCH-006 — Sprint-end: CHANGELOG, version bump, roadmap pointer update

---

## Dependency Map

```
ARCH-005 (no deps)   BE-008 (no deps)   DB-005 (no deps)   BE-007 (no deps)   UI-014 (no deps)   UI-015 (no deps)
                                              |                    |
                                          BE-006                UI-013
                                              |
                                          UI-012
        |__________|__________|__________|__________|__________|__________|
                                          |
                                      QA-003 (dep: BE-008, BE-006, UI-012, BE-007, UI-013, UI-014, UI-015)
                                          |
                                      ARCH-006 (dep: QA-003)
```

---

## Tasks

---

### ARCH-005: Register Sprint 04 Architecture Decisions in AGENTS.md and REQUIREMENTS.md

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

This sprint adds a new DB column, a new PATCH endpoint, a new `lastSeenAtUtc` concept, and a default-on UI behavior change (focused view) — none covered by current File Contracts or REQUIREMENTS.md. Register these before code is written.

---

**Files to read before starting:**

- `AGENTS.md` — File Contracts table and `## Current Sprint` section to extend
- `docs/requirements/REQUIREMENTS.md` — sections 2.4 (Vehicle Detail), 2.6 (API), 5 (Data Model), 6 (PostgreSQL Schema), 9 (Environment Variables) to extend
- `docs/sprints/sprint-04.md` (this file) — full task list

---

**Files to modify:**

- `AGENTS.md` — add File Contracts rows noting: `PATCH /api/vehicles/{id}` never renames the `id` primary key (only `driver_name`/`display_number`); update `## Current Sprint` to point at `sprint-04.md`
- `docs/requirements/REQUIREMENTS.md` — add F-31 (`PATCH /api/vehicles/{id}` contract — driver name + display number only, `id` immutable), F-32 (dummy-mode vehicle IDs MUST use the same `VEH-NNNNN` format as live mode, not random strings), F-33 (sidebar search MUST exclude vehicles with no telemetry activity in the last 24 hours when a query is present), F-34 (sidebar MUST default to a maximum of 10 curated vehicles with a toggle to reveal the full list); update §5.1 (Vehicle data model) to add `displayNumber`/`lastSeenAtUtc`; update §6.1 (`vehicles` table) to add the `display_number` column

---

**Files to create:**

None.

---

**Do NOT touch:**

- Any file under `frontend/` or `backend/` — ARCH never writes application code

---

**Sub-task breakdown:**

- [x] Add File Contracts row(s) for the PATCH endpoint's immutability rule
- [x] Update `## Current Sprint` to reference `sprint-04.md` (already pointed at sprint-04.md prior to this task — left unchanged per instruction)
- [x] Add F-31, F-32, F-33, F-34 to `REQUIREMENTS.md` section 2.6 / 2.3 (as appropriate — F-33/F-34 are search/filtering, section 2.3; F-31/F-32 are API/data, section 2.6)
- [x] Update §5.1 and §6.1 with the new fields

---

**Implementation notes:**

1. F-32 must explicitly call out that the fix targets `TelemetrySimulationService.MakeId()` and that this is NOT a violation of the "in-memory only, no DB/HTTP calls" rule for that file — the ID format changes, no dependency is added.
2. Keep language/table format consistent with existing REQUIREMENTS.md style.

---

**Acceptance criteria:**

1. `AGENTS.md` `## Current Sprint` points to `sprint-04.md`
2. `REQUIREMENTS.md` contains F-31, F-32, F-33, F-34 and updated §5.1/§6.1

---

**Verification command:**

```bash
grep -c "F-31\|F-32\|F-33\|F-34" docs/requirements/REQUIREMENTS.md
# Expected: 4
```

---

**Rollback:**

```bash
git checkout -- AGENTS.md docs/requirements/REQUIREMENTS.md
```

---

### BE-008: Fix Dummy-Mode Vehicle ID Generation to Be Meaningful/Consistent

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`TelemetrySimulationService.MakeId()` (lines ~190-204) generates random 2-3-letter-prefix + 4-6-character-suffix IDs (e.g. `"XJ-4K7Q2"`) for every dummy-mode vehicle. This is inconsistent with the `VEH-00001`..`VEH-09999` format used by live mode, the DB-seeded `vehicles` table, and the Python emitter, and is the root of the operator's "vehicle number not meaningful" complaint for anyone running `dotnet run` without Docker. Fix: generate `VEH-NNNNN` (zero-padded to 5 digits) IDs deterministically from the seeding loop index, matching the live-mode format exactly.

---

**Files to read before starting:**

- `backend/Services/TelemetrySimulationService.cs` — full seeding loop (`MakeId()` and its caller, roughly lines 180-240) — the exact code to change
- `backend/Data/DbSeeder.cs` — confirms the `VEH-{i:D5}` format used by live mode, to mirror exactly

---

**Files to modify:**

- `backend/Services/TelemetrySimulationService.cs` — replace `MakeId()`'s random-string logic with a deterministic `$"VEH-{i:D5}"` format using the seeding loop's index; remove now-unused random-ID-generation code (prefix/suffix character loops)

---

**Files to create:**

None.

---

**Do NOT touch:**

- Anything in this file beyond `MakeId()` and its immediate call site — do not add DB or HTTP calls, do not modify `EvaluateStatus`, do not change the corridor/status-distribution logic
- `backend/Data/DbSeeder.cs` — read-only reference, not modified

---

**Sub-task breakdown:**

- [x] Replace `MakeId()`'s body with `$"VEH-{i:D5}"` (or remove the local function entirely and inline the format at the call site, whichever is the smaller diff) — inlined at the call site, function removed entirely
- [x] Confirm the seeding loop's index variable (`i`) is in scope at the ID-assignment call site
- [x] Run `dotnet build FleetTelemetry.csproj` — zero errors
- [ ] Manually run in dummy mode and confirm `GET /api/vehicles` returns `VEH-00000`..`VEH-09999`-style IDs, not random strings — deferred to QA-003 (Docker stack); local `dotnet run` blocked by the sandbox's known host runtime-patch mismatch

---

**Implementation notes:**

1. This only changes what string is assigned to `Vehicle.Id` during seeding — it does not touch `EvaluateStatus`, the corridor/movement logic, or the distribution-cap logic, all of which key off the `Vehicle` object by reference, not by ID format.
2. `driver` name generation (already a curated 12-name list, already "meaningful") is untouched by this task — only the ID format was the actual problem.
3. Confirm no other code in this file parses/relies on the old ID's specific shape (e.g. a regex expecting a 2-3 letter prefix) before deleting the old generator — search the file for any other reference to the ID format.

---

**Acceptance criteria:**

1. `dotnet build FleetTelemetry.csproj` passes with zero errors
2. With `USE_LIVE_TELEMETRY=false` (dummy mode), `GET /api/vehicles` returns 10,000 vehicles with IDs matching `VEH-\d{5}`, not random strings
3. No other dummy-mode behavior (status distribution, movement, logs) changes

---

**Verification command:**

```bash
cd backend
dotnet build FleetTelemetry.csproj
# Expected: Build succeeded, 0 Error(s)
# Full dotnet run verification against the Docker stack in QA-003 (local dotnet run
# is blocked in this sandbox by a host .NET runtime patch-version mismatch, per
# Sprint 03's BE-005/DB-004 notes)
```

---

**Rollback:**

```bash
git checkout -- backend/Services/TelemetrySimulationService.cs
```

---

### DB-005: Add `display_number` Column to `vehicles`

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

The editable-fields feature (BE-006/UI-012) needs a place to store an operator-edited "vehicle number" distinct from the immutable `id` primary key. This task adds a nullable `display_number VARCHAR(30)` column to `vehicles` via an EF Core migration, updates `VehicleEntity`, and seeds a sensible default (`FL-{i:D5}`) for all 10,000 rows so the field is never blank out of the box.

---

**Files to read before starting:**

- `backend/Data/Entities/VehicleEntity.cs` — entity to extend
- `backend/Data/DbSeeder.cs` — seeding loop to extend with a default value
- `backend/Data/Migrations/20260709060640_InitialSchema.cs` — existing migration style to match
- `docs/requirements/REQUIREMENTS.md` §6.1 (updated by ARCH-005) — exact column name/type expected

---

**Files to modify:**

- `backend/Data/Entities/VehicleEntity.cs` — add `DisplayNumber` property, `[Column("display_number")] [MaxLength(30)]`, nullable
- `backend/Data/DbSeeder.cs` — seed `DisplayNumber = $"FL-{i:D5}"` for each vehicle
- `backend/AGENTS.md` — note the new column in the PostgreSQL Integration section

---

**Files to create:**

- `backend/Data/Migrations/<timestamp>_AddVehicleDisplayNumber/` — auto-generated by `dotnet ef migrations add AddVehicleDisplayNumber`

---

**Do NOT touch:**

- `backend/Data/Migrations/20260709060640_InitialSchema.cs` — never hand-edit an existing applied migration
- `backend/Data/Entities/TelemetrySnapshotEntity.cs`, `backend/Data/Entities/VehicleLogEntity.cs`

---

**Sub-task breakdown:**

- [x] Add `DisplayNumber` to `VehicleEntity.cs`
- [x] Update `DbSeeder.cs` to populate it
- [x] Run `dotnet ef migrations add AddVehicleDisplayNumber` from `backend/` (needed `DOTNET_ROLL_FORWARD=LatestMajor` to work around the sandbox's known host-runtime patch mismatch — migration output itself is generated from the model, unaffected by this)
- [x] Inspect the generated migration file — confirmed it only adds `display_number VARCHAR(30)` nullable, nothing else (verified via both the migration file and the `FleetDbContextModelSnapshot.cs` diff)
- [x] Run `dotnet build FleetTelemetry.csproj` — zero errors
- [ ] `dotnet ef database update` — deferred to app startup against the Docker stack in QA-003, per plan

---

**Implementation notes:**

1. Column must be nullable at the DB level (existing rows before this migration need a value — `DbSeeder` only runs once on an empty table, so for a *fresh* seed the column is always populated, but the migration itself must not assume the table is empty in case it's ever re-run against a partially-seeded DB from a prior sprint's local testing).
2. `[MaxLength(30)]` matches the `VARCHAR(30)` chosen for the format `FL-NNNNN` (7 chars) plus generous headroom for operator-entered values.
3. Existing rows (if any survive from prior local testing) will have `display_number = NULL` until edited — the frontend/API layer, not this migration, decides the display fallback for a null value (BE-006/UI-012's job).

---

**Acceptance criteria:**

1. `dotnet ef migrations add AddVehicleDisplayNumber` succeeds and produces exactly one new migration
2. The migration adds a single nullable `display_number VARCHAR(30)` column, nothing else
3. `dotnet build FleetTelemetry.csproj` passes with zero errors
4. `DbSeeder.cs` populates `display_number` for newly-seeded rows

---

**Verification command:**

```bash
cd backend
dotnet ef migrations add AddVehicleDisplayNumber
dotnet build FleetTelemetry.csproj
# Expected: Build succeeded, 0 Error(s)
# Applied + row-population verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
cd backend
dotnet ef migrations remove
git checkout -- backend/Data/Entities/VehicleEntity.cs backend/Data/DbSeeder.cs backend/AGENTS.md
```

---

### BE-006: Add `PATCH /api/vehicles/{id}` Endpoint

**Agent:** ASP.NET
**Depends on:** DB-005
**Status:** [x]

---

**Context:**

No endpoint currently allows editing a vehicle's driver name or (new) display number. This task adds `PATCH /api/vehicles/{id}` accepting `{ driverName?, displayNumber? }`, validating both, persisting to the Postgres `vehicles` row, and — so the change is visible immediately without restarting the app — also updating whichever in-memory store is currently active (`ILiveTelemetryStore` in live mode, `TelemetrySimulationService.Vehicles` in dummy mode), per this sprint's locked-in decision #2.

---

**Files to read before starting:**

- `backend/Controllers/VehiclesController.cs` — existing controller to extend, read-path branching pattern (`UseLiveTelemetry`) to mirror
- `backend/Data/Entities/VehicleEntity.cs` — updated by DB-005, confirm `DisplayNumber` property name
- `backend/Models/Vehicle.cs` — internal model; needs a `DisplayNumber` field added (append `[Key(11)]`, MessagePack keys are append-only, never renumber existing keys)
- `backend/Models/ApiVehicle.cs` — REST DTO; needs a `displayNumber` field added
- `backend/Services/LiveTelemetryStore.cs` — `Upsert`/`TryGet` API to use for the live-mode in-memory update
- `backend/Data/FleetDbContext.cs` — `DbSet<VehicleEntity>` for the Postgres write

---

**Files to modify:**

- `backend/Models/Vehicle.cs` — add `[Key(11)] public string DisplayNumber { get; set; } = string.Empty;`
- `backend/Models/ApiVehicle.cs` — add `[JsonPropertyName("displayNumber")] public string DisplayNumber { get; set; } = string.Empty;`
- `backend/Controllers/VehiclesController.cs` — add `[HttpPatch("{id}")]` action; map `DisplayNumber`/`Driver` into `ApiVehicle` in `Get`/`List` too
- `backend/AGENTS.md` — add `PATCH /api/vehicles/{id}` to the API Endpoint Map; note `PatchVehicleRequest.cs`

---

**Files to create:**

- `backend/Models/PatchVehicleRequest.cs` — DTO: `DriverName` (string?, optional), `DisplayNumber` (string?, optional)

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs` — read/write its public static `Vehicles` dictionary from the controller; do not add methods to the service class itself
- `backend/Hubs/FleetHub.cs`, `backend/Services/LiveBroadcastService.cs` — an edit does not need a SignalR broadcast; the PATCH response itself carries the updated vehicle back to the caller
- `backend/Controllers/LogsController.cs`, `backend/Controllers/MetadataController.cs`, `backend/Controllers/TelemetryIngestController.cs`

---

**Sub-task breakdown:**

- [x] Add `DisplayNumber` to `Vehicle.cs` (`[Key(11)]`) and `ApiVehicle.cs`
- [x] Create `PatchVehicleRequest.cs`
- [x] Add `PATCH /api/vehicles/{id}` to `VehiclesController`: validate (400 if both fields absent/empty, 400 if either exceeds its DB column's max length — 100 for driver name, 30 for display number), look up the vehicle in the currently-active in-memory store (404 if not found), apply the provided field(s), persist to Postgres via `FleetDbContext` (`db.Vehicles.FindAsync(id)`, update, `SaveChangesAsync`), return `200 OK` with the updated `ApiVehicle`
- [x] Update `Get`/`List` actions to map `DisplayNumber` into `ApiVehicle` alongside the existing fields
- [x] Run `dotnet build FleetTelemetry.csproj` — zero errors
- [ ] Manually PATCH a vehicle and confirm both the Postgres row and a subsequent `GET` reflect the change — deferred to QA-003 (Docker stack); local curl testing blocked by the sandbox's known host runtime-patch mismatch

---

**Implementation notes:**

1. `id` in the route is never mutated — the request body has no `id`/`vehicleId` field, only `driverName`/`displayNumber`. This is deliberate (decision #1) — do not add an ID-rename path.
2. Postgres write happens synchronously in the controller here (unlike `TelemetryIngestController`'s buffered-channel pattern) — this is a rare, low-frequency admin action (an operator editing one vehicle), not a high-throughput ingestion path, so a direct `SaveChangesAsync` is appropriate and matches the low-volume nature of the operation; do not build a channel/queue for this.
3. In dummy mode, `TelemetrySimulationService.Vehicles[id]` is a `ConcurrentDictionary<string, Vehicle>` — look up and mutate the `Vehicle` object's `DriverName`/`DisplayNumber` properties in place (the same object the background loop's `Parallel.ForEach` also mutates concurrently — property-level writes to `string` fields are not torn under concurrent access in .NET, this is safe without additional locking, consistent with how the existing simulation loop already mutates other fields concurrently).
4. In dummy mode there is still a Postgres write to the `vehicles` table (the `id` exists there since `DbSeeder` always seeds all 10,000 rows regardless of `USE_LIVE_TELEMETRY`) — the edit persists across a restart in both modes, only the *live-reflected* in-memory copy differs by mode.
5. Response body: the updated `ApiVehicle`, same shape as `GET /api/vehicles/{id}`'s `vehicle` field — keeps the frontend's update logic simple (one shape to merge into local state either way).

---

**Acceptance criteria:**

1. `dotnet build FleetTelemetry.csproj` passes with zero errors
2. `PATCH /api/vehicles/VEH-00001` with `{"driverName":"Test Driver"}` returns `200 OK` with the updated vehicle
3. A subsequent `GET /api/vehicles/VEH-00001` reflects the new driver name
4. `PATCH` with an empty body (`{}`) returns `400 Bad Request`
5. `PATCH /api/vehicles/VEH-99999` (nonexistent) returns `404 Not Found`
6. The `id` field in the response never changes regardless of request body content

---

**Verification command:**

```bash
cd backend
dotnet build FleetTelemetry.csproj
# Expected: Build succeeded, 0 Error(s)
# Live PATCH/GET round-trip verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
git checkout -- backend/Models/Vehicle.cs backend/Models/ApiVehicle.cs backend/Controllers/VehiclesController.cs backend/AGENTS.md
git rm backend/Models/PatchVehicleRequest.cs
```

---

### UI-012: Add Vehicle Edit UI to DetailPanel

**Agent:** NEXT
**Depends on:** BE-006
**Status:** [x]

---

**Context:**

Operators need a way to trigger the new `PATCH /api/vehicles/{id}` endpoint from the dashboard. This task adds an inline edit affordance to `DetailPanel.tsx` for the driver name and display number, with basic client-side validation and optimistic local state update on success.

---

**Files to read before starting:**

- `frontend/components/DetailPanel.tsx` — panel to extend with edit UI
- `frontend/types/vehicle.ts` — `Vehicle` type; needs `displayNumber?: string` added
- `frontend/app/page.tsx` — where `vehiclesMap`/`setVehicles` live, needed to merge a PATCH response back into local state
- `frontend/AGENTS.md` — coding conventions (no `any`, `NEXT_PUBLIC_API_URL` usage)

---

**Files to modify:**

- `frontend/types/vehicle.ts` — add `displayNumber?: string`
- `frontend/components/DetailPanel.tsx` — add an edit button/inline form for driver name + display number, `fetch(PATCH ...)`, loading/error state, calls a new `onVehicleUpdated` callback prop on success
- `frontend/app/page.tsx` — pass an `onVehicleUpdated` callback into `<DetailPanel />` that merges the PATCH response into `vehiclesMap.current` and flushes `setVehicles`

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/data/DUMMY.json`
- `frontend/components/Sidebar.tsx`, `frontend/components/MapView.tsx` — out of this task's scope (they only need the `displayNumber` type to exist, not new UI)

---

**Sub-task breakdown:**

- [x] Add `displayNumber?: string` to `Vehicle` type
- [x] Add edit UI to `DetailPanel.tsx`: an "Edit" button toggles two inline text inputs (driver name, display number) pre-filled with current values; "Save"/"Cancel" buttons
- [x] On "Save": client-side validate both fields non-empty and within length limits (100/30 chars, matching the backend), `PATCH` to `${API_URL}/api/vehicles/${vehicle.id}` with only the changed field(s), show a loading state, show an inline error message on failure (400/404/network), call `onVehicleUpdated(updatedVehicle)` on success
- [x] Add `onVehicleUpdated` prop to `DetailPanel`'s `Props` type
- [x] Wire `onVehicleUpdated` in `page.tsx`: update `vehiclesMap.current.set(id, {...existing, ...updatedFields})`, flush `setVehicles`, and if the edited vehicle is `selected`, update `selected` too so the panel reflects the save immediately
- [x] Run `npx tsc --noEmit` — zero errors

---

**Implementation notes:**

1. Only send the field(s) that actually changed in the PATCH body — do not always send both `driverName` and `displayNumber` even if only one was edited (matches the backend's optional-field contract).
2. Map the PATCH JSON response (`{ id, model, driver, status, fuel, temp, speedKph, cargoLoad, lat, lng, displayNumber }`) back into the frontend's `Vehicle` shape the same way `page.tsx`'s initial load already does (`driver` → `driver`, not `driverName` — the API DTO uses the short field names already established in `ApiVehicle.cs`).
3. Keep the edit form minimal — two text inputs, no rich validation UI, consistent with the existing dashboard's dense/functional aesthetic (see `Sidebar.tsx`'s existing filter controls for the established input styling).

---

**Acceptance criteria:**

1. `npx tsc --noEmit` passes with zero errors
2. Clicking "Edit" in `DetailPanel` reveals editable driver-name/display-number fields pre-filled with current values
3. Saving a valid edit updates the panel and the corresponding Sidebar row without a page reload
4. Saving an empty field shows a client-side validation error and does not call the API
5. A failed PATCH (e.g. network error) shows an inline error message and does not silently fail

---

**Verification command:**

```bash
cd frontend
npx tsc --noEmit
# Expected: zero errors
# Live edit-and-persist flow verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
git checkout -- frontend/types/vehicle.ts frontend/components/DetailPanel.tsx frontend/app/page.tsx
```

---

### BE-007: Expose `lastSeenAtUtc` Per Vehicle

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

The 24h-activity search filter (UI-013) needs a per-vehicle "last seen" timestamp available to the frontend. `ILiveTelemetryStore` currently tracks a `_dirty` flag per vehicle but no timestamp of when a vehicle was last upserted. This task adds that timestamp to the live store and exposes it via the existing `GET /api/vehicles`/`GET /api/vehicles/{id}` response shape. In dummy mode, vehicles are always "live" (the simulation ticks continuously) — `lastSeenAtUtc` there is simply the current server time on every request, not tracked historically.

---

**Files to read before starting:**

- `backend/Services/LiveTelemetryStore.cs` — `Upsert`/`_dirty` pattern to extend with a timestamp
- `backend/Controllers/VehiclesController.cs` — `Get`/`List` actions to extend with the new field
- `backend/Models/ApiVehicle.cs` — DTO to extend

---

**Files to modify:**

- `backend/Services/LiveTelemetryStore.cs` — add a `ConcurrentDictionary<string, DateTime> _lastSeenUtc`, updated in `Upsert`; add `bool TryGetLastSeenUtc(string id, out DateTime lastSeenUtc)` to `ILiveTelemetryStore`
- `backend/Models/ApiVehicle.cs` — add `[JsonPropertyName("lastSeenAtUtc")] public DateTime LastSeenAtUtc { get; set; }`
- `backend/Controllers/VehiclesController.cs` — populate `LastSeenAtUtc` in `Get`/`List`: live mode reads `_liveStore.TryGetLastSeenUtc`, dummy mode uses `DateTime.UtcNow` (always "just seen")
- `backend/AGENTS.md` — note the new field in the API Endpoint Map description

---

**Files to create:**

None.

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs`
- `backend/Services/TelemetryPersistenceService.cs`, `backend/Services/LiveBroadcastService.cs`

---

**Sub-task breakdown:**

- [x] Add `_lastSeenUtc` dictionary and `TryGetLastSeenUtc` to `LiveTelemetryStore`/`ILiveTelemetryStore`, updated inside `Upsert`
- [x] Add `LastSeenAtUtc` to `ApiVehicle`
- [x] Populate it in `VehiclesController.Get`/`.List`/`.Patch` (branching on `UseLiveTelemetry`, same pattern as every other field)
- [x] Run `dotnet build FleetTelemetry.csproj` — zero errors

---

**Implementation notes:**

1. `_lastSeenUtc` is updated inside `Upsert` alongside `_dirty` — one extra dictionary write per ingest, negligible cost.
2. Dummy mode's `DateTime.UtcNow` fallback is intentional and matches this sprint's decision #3's spirit — the simulation never truly "goes stale" the way a live-ingested vehicle whose emitter died would, so there is no meaningful historical timestamp to track there without adding new state to `TelemetrySimulationService.cs` (out of scope, `Do NOT touch`).

---

**Acceptance criteria:**

1. `dotnet build FleetTelemetry.csproj` passes with zero errors
2. `GET /api/vehicles` response includes a `lastSeenAtUtc` field on every vehicle
3. In live mode, a vehicle's `lastSeenAtUtc` updates after a new `POST /api/telemetry/ingest` for that vehicle

---

**Verification command:**

```bash
cd backend
dotnet build FleetTelemetry.csproj
# Expected: Build succeeded, 0 Error(s)
# Live-mode timestamp update verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
git checkout -- backend/Services/LiveTelemetryStore.cs backend/Models/ApiVehicle.cs backend/Controllers/VehiclesController.cs backend/AGENTS.md
```

---

### UI-013: Apply 24h-Activity Filter to Sidebar Search

**Agent:** NEXT
**Depends on:** BE-007
**Status:** [x]

---

**Context:**

Per this sprint's decision #3, when the sidebar search box has a non-empty query, results should additionally exclude vehicles with no activity (`lastSeenAtUtc`) in the last 24 hours. This task wires the new `lastSeenAtUtc` field (BE-007) into `Sidebar.tsx`'s existing search/filter pipeline.

---

**Files to read before starting:**

- `frontend/components/Sidebar.tsx` — existing `filtered` `useMemo` (token-index search + status filter) to extend
- `frontend/types/vehicle.ts` — needs `lastSeenAtUtc?: string` added
- `frontend/app/page.tsx` — initial load's field-normalization mapping (`arr: Vehicle[] = ...`) to extend

---

**Files to modify:**

- `frontend/types/vehicle.ts` — add `lastSeenAtUtc?: string` (ISO timestamp string, as returned by the API)
- `frontend/app/page.tsx` — map `v.lastSeenAtUtc` into the normalized `Vehicle` shape on initial load
- `frontend/components/Sidebar.tsx` — in the `filtered` `useMemo`, when `query` is non-empty, additionally filter out vehicles whose `lastSeenAtUtc` is older than 24 hours; display a compact "last seen" indicator on search-result rows

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/components/MapView.tsx`, `frontend/components/DetailPanel.tsx` — out of scope for this task
- Any file under `backend/`

---

**Sub-task breakdown:**

- [x] Add `lastSeenAtUtc?: string` to `Vehicle` type
- [x] Map it through in `page.tsx`'s initial-load normalization
- [x] In `Sidebar.tsx`'s `filtered` memo, after the existing token-search/status-filter logic, add the 24h exclusion, only applied when there's an active search query
- [x] Add a small "last seen" relative-time label to matched rows when a search query is active
- [x] Run `npx tsc --noEmit` — zero errors

---

**Implementation notes:**

1. The 24h filter only applies when `q` (the debounced search query) is non-empty — browsing the unfiltered/status-filtered list is unaffected, matching decision #3 ("search... additionally filtered", not a standing filter).
2. `!v.lastSeenAtUtc` (missing field) is treated as "don't exclude" — defensive default in case a vehicle predates this field or the API response is momentarily incomplete, so search never silently hides everything due to a missing field.
3. Keep the relative-time formatting simple (a small inline helper — minutes/hours/days ago) — no new date library dependency.

---

**Acceptance criteria:**

1. `npx tsc --noEmit` passes with zero errors
2. Searching for a vehicle ID/driver name with recent activity (< 24h) returns it in results
3. Searching for a vehicle ID/driver name with no activity in 24h+ excludes it from results
4. Clearing the search query restores the full (status-filtered) list, unaffected by the 24h rule

---

**Verification command:**

```bash
cd frontend
npx tsc --noEmit
# Expected: zero errors
# Live search-filter behavior verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
git checkout -- frontend/types/vehicle.ts frontend/app/page.tsx frontend/components/Sidebar.tsx
```

---

### UI-014: Add Default-On Focused View (Max 10 Vehicles + "Show All" Toggle)

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Context:**

Per this sprint's decision #4, the sidebar should default to showing at most 10 curated vehicles (highest-priority by status, matching the existing `danger > warning > offline > active` sort order already in `Sidebar.tsx`) instead of the full 10,000-vehicle list, with a "Show all" toggle to reveal everything. This builds directly on Sprint 03's `inactive`/`hideInactive` mechanism (`UI-011`) already in `useFilterStore.ts` and `Sidebar.tsx`.

---

**Files to read before starting:**

- `frontend/store/useFilterStore.ts` — existing `hideInactive` pattern (Sprint 03) to extend with a new `focusedView` flag
- `frontend/components/Sidebar.tsx` — `filtered` memo and render logic to extend

---

**Files to modify:**

- `frontend/store/useFilterStore.ts` — add `focusedView: boolean` (default `true`) and `toggleFocusedView()`
- `frontend/components/Sidebar.tsx` — when `focusedView` is `true` and there is no active search query, slice `filtered` to the top 10 (after existing status/hideInactive filtering and priority sort); add a "Show all 10,000" toggle button/label near the existing filter controls, showing the current mode and total count

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/components/MapView.tsx` — the map continues to receive the full `mapVehicles` list from `page.tsx` unchanged; the focused view is a Sidebar-list-only concept, not a map-visibility concept
- Any file under `backend/`

---

**Sub-task breakdown:**

- [x] Add `focusedView`/`toggleFocusedView` to `useFilterStore.ts`, default `true`
- [x] In `Sidebar.tsx`'s `filtered` memo (or immediately after it, before virtualization), when `focusedView && !query`, take only the first 10 entries of the already-sorted/filtered list — implemented as a derived `visible` memo, with the virtualizer/keyboard-nav/row-lookup all switched from `filtered` to `visible`
- [x] Add a toggle control ("Focused View (Top 10)" / "Show All {N}") near the existing "Hide Inactive" checkbox
- [x] When a search query is active, focused view's 10-item cap does NOT apply
- [x] Run `npx tsc --noEmit` — zero errors

---

**Implementation notes:**

1. The 10-item cap is applied AFTER status filtering, hide-inactive filtering, and priority sorting — so the 10 shown are genuinely the highest-priority/most-relevant vehicles, not an arbitrary slice.
2. `focusedView` and `hideInactive` are independent toggles — an operator can have focused view on (max 10) while still choosing to include or exclude inactive vehicles from consideration for those 10 slots.
3. This does not change `MapView`'s vehicle count (NF-01 still applies to the map/overall render) — only the Sidebar's default list length changes.

---

**Acceptance criteria:**

1. `npx tsc --noEmit` passes with zero errors
2. With no search query and default settings, the Sidebar shows at most 10 vehicles
3. Clicking "Show all 10,000" reveals the full (status/hide-inactive-filtered) list, still virtualized
4. Entering a search query shows all matches regardless of the focused-view cap
5. Toggling back to focused view re-applies the 10-item cap

---

**Verification command:**

```bash
cd frontend
npx tsc --noEmit
# Expected: zero errors
# Live toggle behavior verified against the Docker stack in QA-003
```

---

**Rollback:**

```bash
git checkout -- frontend/store/useFilterStore.ts frontend/components/Sidebar.tsx
```

---

### UI-015: Responsive/Overflow Audit and Fix

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Verification note:** Live-verified via headless Chrome DevTools Protocol with
`Emulation.setDeviceMetricsOverride` (not just a code audit) — confirmed real horizontal
overflow at 375px (`docScrollWidth` 775 vs `innerWidth` 375) and 768px (774 vs 768) before the
fix, and `hasHorizOverflow: false` at 375px/768px/1024px/1280px after. Desktop (1280px)
screenshot after the fix is visually identical to before. Also found and fixed a second overflow
inside `DetailPanel` (the `CircularChart`/driver-model grids) not anticipated in the original
task text.

---

**Context:**

Per this sprint's decision #5 (interpreted absent a screenshot from the operator — flag after this sprint if this doesn't match what was meant), this task audits `Header`, `Sidebar`, `MapView`, and `DetailPanel` at mobile (375px) and tablet (768px) viewport widths for horizontal overflow, clipped content, or broken layout, and applies Tailwind responsive utility fixes.

---

**Files to read before starting:**

- `frontend/components/Header.tsx`, `frontend/components/Sidebar.tsx`, `frontend/components/MapView.tsx`, `frontend/components/DetailPanel.tsx` — current layout classes
- `frontend/app/page.tsx` — top-level flex layout wrapping all four
- `frontend/AGENTS.md` — Tailwind theme tokens, styling conventions ("Tailwind utility classes only")

---

**Files to modify:**

- `frontend/components/Header.tsx` — ensure the title/nav/status-indicator/bell/avatar row wraps or truncates gracefully instead of overflowing at narrow widths
- `frontend/components/Sidebar.tsx` — fixed `w-80` (320px) sidebar likely needs to become an overlay/full-width panel below a breakpoint rather than permanently consuming most of a 375px viewport
- `frontend/components/DetailPanel.tsx` — similarly likely needs to become a full-width overlay on narrow viewports instead of a fixed-width side panel
- `frontend/components/MapView.tsx` — confirm it doesn't force a minimum width that causes the page to scroll horizontally

---

**Files to create:**

None.

---

**Do NOT touch:**

- `frontend/data/DUMMY.json`
- Any file under `backend/`
- Desktop-width (≥1024px) layout/behavior — this task must not regress the existing desktop experience, only add narrower-viewport handling

---

**Sub-task breakdown:**

- [x] Use the `chrome` skill/tool (or manual `npm run dev` + browser resize) to identify concrete overflow/clipping issues at 375px and 768px before making changes — done via direct CDP `Emulation.setDeviceMetricsOverride`, not the `chrome` skill's MCP tools (not registered in this environment)
- [x] Fix `Header.tsx`: `flex-wrap md:flex-nowrap`, truncated/shrinking title, "System Design" link hidden below `md:`
- [x] Fix `Sidebar.tsx`: `w-80` → `w-full md:w-80`, plus `h-[45vh] md:h-auto` for a bounded height when stacked
- [x] Fix `DetailPanel.tsx`: fixed full-screen overlay (`fixed inset-0 z-30 w-full`) below `md:`, reverting to the original static `w-96` side panel at `md:`+; also fixed a nested overflow in its internal grids
- [x] Confirm `MapView.tsx` and the overall `page.tsx` flex container don't force horizontal scroll — `MapView` needed no changes (`flex-1`, no forced min-width); `page.tsx`'s top-level container changed `flex` → `flex flex-col md:flex-row` (necessary, not just a check — a row layout couldn't fit all 3 panels without overflow below `md:`)
- [x] Re-check all 4 components at 375px/768px after fixes — confirmed `hasHorizOverflow: false` via CDP at both, and at 1024px/1280px (desktop unaffected)
- [x] Run `npx tsc --noEmit` — zero errors

---

**Implementation notes:**

1. Desktop layout (the primary use case for a fleet-operations dashboard) must not regress — all changes should be additive via responsive (`sm:`/`md:`) Tailwind prefixes, not changes to the unprefixed (mobile-first base / desktop-inherited) classes where that would alter ≥1024px behavior.
2. This is a best-effort interpretation of an underspecified request (see this sprint's Context) — do not over-invest in pixel-perfect mobile polish; the bar is "no overflow/clipping, usable," not a full mobile redesign.
3. If a toggle/drawer pattern is added for `Sidebar`/`DetailPanel` on narrow viewports, keep it simple (a button that shows/hides, no new animation library) — `framer-motion` is already a dependency if a simple transition is wanted, but is not required.

---

**Acceptance criteria:**

1. `npx tsc --noEmit` passes with zero errors
2. At 375px viewport width, no component causes horizontal page scroll and no interactive element (buttons, inputs) is clipped or inaccessible
3. At 768px viewport width, same as above
4. Desktop (≥1024px) layout is visually unchanged from before this task

---

**Verification command:**

```bash
cd frontend
npx tsc --noEmit
# Expected: zero errors
# Live 375px/768px/desktop visual check performed via the chrome skill in QA-003
```

---

**Rollback:**

```bash
git checkout -- frontend/components/Header.tsx frontend/components/Sidebar.tsx frontend/components/MapView.tsx frontend/components/DetailPanel.tsx
```

---

### BE-009: Fix PATCH Edits Clobbered by the Next Ingest Tick (QA-003 Finding)

**Agent:** ASP.NET
**Depends on:** BE-006, BE-007 (fixes a regression surfaced by their interaction with the live ingestion pipeline)
**Status:** [x]

**Note (ad hoc task, analogous to `IIOT-S02-BE-004`):** Not part of the original 11-task plan.
QA-003's first verification pass found a real, sprint-blocking bug and this task fixes it before
QA-003 could pass, mirroring how Sprint 02's QA-001 found and got `BE-004` fixed mid-sprint.

---

**Context:**

QA-003's first pass (curl + live browser check) found that `PATCH /api/vehicles/{id}` (BE-006) appeared to work — `200 OK`, correct DB row, correct immediate `GET` — but the edit silently reverted within one ingest tick interval (a few seconds under live load). Root cause in `backend/Controllers/TelemetryIngestController.cs`'s `Ingest` action: `DriverName = request.DriverName ?? previous?.DriverName ?? string.Empty` — the Python emitter always sends a non-null `driverName` (its own immutable roster-seeded value), so the `?? previous?.DriverName` fallback never actually triggered, and `DisplayNumber` wasn't referenced in the object initializer at all, so it reset to `""` on every single ingest tick regardless of any PATCH. Every `POST /api/telemetry/ingest` call replaces the vehicle's entire in-memory `Vehicle` object in `ILiveTelemetryStore`, discarding BE-006's in-place edit the moment the next tick arrived — which defeats BE-006/UI-012 entirely under live/Docker conditions, the sprint's primary target environment.

---

**Files to read before starting:**

- `backend/Controllers/TelemetryIngestController.cs` — the `Ingest` action's `Vehicle` object initializer, the exact bug location

---

**Files to modify:**

- `backend/Controllers/TelemetryIngestController.cs` — flip field priority so the live store's existing state (potentially PATCH-edited) wins over the incoming ingest request for `DriverName`/`DisplayNumber` specifically

---

**Files to create:**

None.

---

**Do NOT touch:**

- Anything else in this file — validation logic, `Model` field mapping, log-message building, the buffered-write/channel pattern — this is a surgical two-line fix

---

**Sub-task breakdown:**

- [x] Change `DriverName = request.DriverName ?? previous?.DriverName ?? string.Empty` to `DriverName = previous?.DriverName ?? request.DriverName ?? string.Empty`
- [x] Add `DisplayNumber = previous?.DisplayNumber ?? string.Empty` to the same object initializer
- [x] Run `dotnet build FleetTelemetry.csproj` — zero errors
- [x] Rebuild + restart the `backend` Docker container, re-verify: PATCH a vehicle, wait through several ingest ticks (~12s at this sandbox's `TICK_INTERVAL_SECONDS=2`), confirm the edit survives while telemetry fields (fuel/temp/speed/lat/lng/`lastSeenAtUtc`) keep updating normally
- [x] Re-verify 400 (empty body) / 404 (nonexistent vehicle) still behave correctly
- [x] Re-verify the DB row itself reflects the edit (`SELECT driver_name, display_number FROM vehicles WHERE id=...`)

---

**Implementation notes:**

1. Once a vehicle has ANY prior state in `ILiveTelemetryStore` (true after its first-ever ingest, which happens within seconds of the emitter starting), `DriverName`/`DisplayNumber` become sticky — only `PATCH /api/vehicles/{id}` changes them from then on. First-ever ingest (no `previous`) still seeds `DriverName` from the emitter's initial value, unchanged from prior behavior.
2. Known, separate, smaller gap intentionally left unfixed here: `ILiveTelemetryStore` is never hydrated from Postgres's DB-seeded `display_number` (`FL-NNNNN`) on backend startup — a freshly-started live-mode backend shows `displayNumber: ""` for every vehicle until an operator PATCHes one in, rather than the DB-seeded default. Tracked as a follow-up in `docs/sprints/BACKLOG.md`; out of scope for this specific fix, which targets the data-loss/clobbering bug, not the cold-start-hydration gap.

---

**Acceptance criteria:**

1. `dotnet build FleetTelemetry.csproj` passes with zero errors
2. A `PATCH`-edited `driverName`/`displayNumber` survives at least 3 subsequent ingest ticks for that vehicle while other telemetry fields continue updating normally
3. `400`/`404` validation behavior is unchanged
4. The Postgres `vehicles` row reflects the edit

---

**Verification command:**

```bash
curl -s -X PATCH http://localhost:8080/api/vehicles/VEH-00001 -H "Content-Type: application/json" -d '{"driverName":"QA Test Driver 2","displayNumber":"FL-99999"}'
sleep 12
curl -s http://localhost:8080/api/vehicles/VEH-00001 | python -m json.tool
# Expected: driver == "QA Test Driver 2", displayNumber == "FL-99999", still present after 12s
# of live ingest ticks (confirmed by fuel/temp/speed/lat/lng/lastSeenAtUtc having changed)
```

---

**Rollback:**

```bash
git checkout -- backend/Controllers/TelemetryIngestController.cs
```

---

### QA-003: Verify Sprint 04 End-to-End

**Agent:** QA
**Depends on:** BE-008, BE-006, UI-012, BE-007, UI-013, UI-014, UI-015, BE-009
**Status:** [x]

---

**Context:**

Confirms the sprint's success metric holds against the running Docker stack: dummy-mode IDs are meaningful, the edit flow persists and reflects live, search respects the 24h rule, focused view defaults correctly, and the responsive fixes hold at the target breakpoints.

---

**Verification results (final pass, all 8 acceptance criteria PASS):**

Scale used: local `docker-compose.override.yml` (uncommitted), `VEHICLE_COUNT=300`, `TICK_INTERVAL_SECONDS=2` — reduced from the production 10,000/3s default for sandbox feasibility, doesn't affect correctness.

First pass found AC3 (PATCH-and-reflect) **failing**: a PATCH succeeded and reflected in the immediate next `GET`, but reverted within one ingest tick (~2-3s under live load) — the edit was silently discarded by `TelemetryIngestController` rebuilding a fresh `Vehicle` object from every ingest tick. Root-caused and fixed as `BE-009` (ad hoc, mirroring `IIOT-S02-BE-004`'s precedent): flipped field priority so `ILiveTelemetryStore`'s existing state wins over the incoming ingest request for `DriverName`/`DisplayNumber` specifically. Re-verified on the rebuilt `backend` container:

1. **All services healthy** — `db`/`backend`/`frontend` "Up (healthy)", `iiot-emitter` "Up" (no healthcheck by design). `npx tsc --noEmit` and `dotnet build FleetTelemetry.csproj` both zero errors.
2. **`display_number` populated** — `\d vehicles` shows the column; `SELECT display_number FROM vehicles LIMIT 3` → `FL-00000`, `FL-00001`, `FL-00002`.
3. **PATCH-and-reflect: PASS after BE-009.** `PATCH VEH-00001 {driverName, displayNumber}` → `200`; immediate `GET` reflects it; **re-checked after 12s (≈6 ingest ticks)** — driver/displayNumber still correct while fuel/temp/speed/lat/lng/`lastSeenAtUtc` continued updating normally, proving the fix holds under live ingestion, not just at rest. `400` on empty body and `404` on nonexistent vehicle both confirmed unchanged. DB row confirmed via `psql`.
4. **Dummy-mode IDs `VEH-NNNNN`: PASS, verified via code review** — `TelemetrySimulationService.cs:190`, `var id = $"VEH-{i:D5}";`. Local `dotnet run` blocked by this sandbox's known host .NET runtime-patch mismatch (8.0.28 required, 8.0.23 installed); code review used per that established limitation.
5. **24h search filter: PASS, verified via code review** — `Sidebar.tsx`'s `filtered` memo excludes stale (>24h) results only when a query is active, missing timestamp defaults to "don't exclude." A live stale (>24h) test vehicle wasn't synthesized (would require direct DB manipulation beyond QA's read-only mandate).
6. **Focused view: PASS** — CDP-driven interactive check confirmed the "Focused View (Top 10)" / "Show All 300" toggle (count matches the local `VEHICLE_COUNT=300`) both cap and reveal the list correctly.
7. **No horizontal overflow at 375px/768px: PASS** — spot-checked via `scrollWidth`/`innerWidth` equality at both breakpoints (UI-015's own implementation already did the rigorous CDP before/after verification).
8. **No console errors: PASS** — only a pre-existing Zustand deprecation notice observed across the full session (navigation, toggle, edit/save, reload).

**Known follow-up (not a blocker):** `ILiveTelemetryStore` isn't hydrated from Postgres's DB-seeded `display_number` on backend startup — a freshly-started live-mode backend shows `displayNumber: ""` until an operator PATCHes one in. Tracked in `docs/sprints/BACKLOG.md`.

---

**Files to read before starting:**

- `docs/sprints/sprint-04.md` (this file) — Sprint Metadata "Success metric" and all task Acceptance Criteria
- `docs/requirements/REQUIREMENTS.md` — F-31 through F-34, updated §5.1/§6.1

---

**Files to modify:**

None.

---

**Files to create:**

None (report findings back to the user; do not create a report file unless explicitly asked).

---

**Do NOT touch:**

- Any production source file — QA verifies and reports, does not fix

---

**Sub-task breakdown:**

- [x] `cd frontend && npx tsc --noEmit` — zero errors
- [x] `cd backend && dotnet build FleetTelemetry.csproj` — zero errors
- [x] `docker-compose up --build -d` (or confirm already-healthy stack) — all services healthy
- [x] Confirm the `display_number` migration applied (`\d vehicles` in `psql`) and rows are populated
- [x] `PATCH` a live vehicle's driver name/display number via `curl`, confirm `200`, confirm a subsequent `GET` reflects it — found it did NOT survive past one ingest tick, root-caused and fixed as `BE-009`, re-verified holding after 12s/~6 ticks
- [x] Open the dashboard, edit a vehicle via `DetailPanel`, confirm the Sidebar row updates without reload
- [x] Confirm dummy-mode IDs follow `VEH-NNNNN` — verified via code review (local `dotnet run` blocked by sandbox runtime mismatch)
- [x] Search for a vehicle with recent activity — confirm it appears; 24h exclusion verified via code review (no live stale vehicle synthesized)
- [x] Confirm the Sidebar defaults to ≤10 vehicles and "Show all" reveals the full list
- [x] Check `Header`/`Sidebar`/`MapView`/`DetailPanel` at 375px and 768px — no horizontal overflow, no clipped elements
- [x] Confirm no browser console errors

---

**Implementation notes:**

1. If any check fails, do not attempt a fix — report the specific failing check, exact command/output, and which task's Acceptance Criteria it violates, back to the user.
2. Reuse the local `docker-compose.override.yml` pattern from Sprint 03 (reduced `VEHICLE_COUNT`) if a full 10,000-vehicle run isn't necessary for these checks — note the scale used in the report.

---

**Acceptance criteria:**

1. All services healthy after `docker-compose up --build -d`
2. `display_number` column exists and is populated
3. PATCH-and-reflect round trip works via both `curl` and the live UI
4. Dummy-mode vehicle IDs follow `VEH-NNNNN` (verified live or via code review, whichever the environment allows)
5. 24h search filter behaves correctly (or is verified via code review if a stale test vehicle can't be feasibly produced live)
6. Focused view defaults to ≤10 vehicles with a working "Show all" toggle
7. No horizontal overflow/clipping at 375px or 768px on any of the 4 audited components
8. No console errors

---

**Verification command:**

```bash
docker-compose up --build -d
sleep 60
docker-compose ps

curl -s -X PATCH http://localhost:8080/api/vehicles/VEH-00001 \
  -H "Content-Type: application/json" -d '{"driverName":"QA Test Driver"}'
curl -s http://localhost:8080/api/vehicles/VEH-00001 | python -m json.tool
# Expected: driver == "QA Test Driver" in the second response

curl -s http://localhost:8080/api/vehicles | python -c "import json,sys; d=json.load(sys.stdin); print(d[0].get('displayNumber'), d[0].get('lastSeenAtUtc'))"
# Expected: both fields present and non-null for a live-mode vehicle
```

---

**Rollback:**

Not applicable — verification-only task, no files modified.

---

### ARCH-006: Sprint-End — CHANGELOG, Version Bump, Roadmap Pointer Update

**Agent:** ARCH
**Depends on:** QA-003
**Status:** [ ]

---

**Context:**

Closes out Sprint 04: documents the shipped features in `CHANGELOG.md`, bumps the frontend version, points `AGENTS.md` at Sprint 05 (Task 9 only, per `docs/sprints/BACKLOG.md` — Task 7 already shipped standalone).

---

**Files to read before starting:**

- `CHANGELOG.md` — current format/most recent entry
- `docs/sprints/BACKLOG.md` — Sprint 05 remaining scope
- `frontend/package.json` — current version

---

**Files to modify:**

- `CHANGELOG.md` — add `## v0.4.0 — 2026-07-20` entry
- `frontend/package.json` — bump version (minor — user-visible features)
- `AGENTS.md` — update `## Current Sprint`: Sprint 04 archived, point to Sprint 05 / `BACKLOG.md`

---

**Files to create:**

None.

---

**Do NOT touch:**

- Any file under `frontend/` other than `package.json`'s version field
- Any file under `backend/`

---

**Sub-task breakdown:**

- [ ] Add `## v0.4.0 — 2026-07-20` to `CHANGELOG.md` with `### Add` (editable vehicle driver/display-number via `PATCH /api/vehicles/{id}`, `display_number` column) and `### Fix` (dummy-mode vehicle IDs now `VEH-NNNNN` instead of random strings) and `### Update` (24h-activity search filter, default-on focused view, responsive fixes) sections
- [ ] Bump `frontend/package.json` version (minor bump)
- [ ] Update `AGENTS.md` `## Current Sprint`: Sprint 04 archived, point to `docs/sprints/BACKLOG.md` for Sprint 05
- [ ] Move `docs/sprints/sprint-04.md` → `docs/sprints/archive/sprint-04.md` (`git mv`)

---

**Implementation notes:**

1. Confirm `CHANGELOG.md`'s top version matches `frontend/package.json`'s version exactly.
2. Note in the changelog if QA-003 found any deviation from full live verification (e.g. dummy-mode ID check done via code review instead of live `dotnet run`, per the sandbox's known runtime-mismatch limitation) — same transparency precedent as Sprint 03.

---

**Acceptance criteria:**

1. `CHANGELOG.md` has a new top entry matching `frontend/package.json`'s version
2. `AGENTS.md` `## Current Sprint` reflects Sprint 04 archived, points to `BACKLOG.md`
3. `docs/sprints/archive/sprint-04.md` exists

---

**Verification command:**

```bash
head -10 CHANGELOG.md
grep -c "BACKLOG.md" AGENTS.md
```

---

**Rollback:**

```bash
git checkout -- CHANGELOG.md frontend/package.json AGENTS.md
git mv docs/sprints/archive/sprint-04.md docs/sprints/sprint-04.md
```

---

## Sprint-End Checklist

**Version and changelog:**
- [ ] Bump `frontend/package.json` version (minor bump)
- [ ] Add `## v0.4.0 — 2026-07-20` entry to `CHANGELOG.md`
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S04-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npx tsc --noEmit` passes on the final branch state
- [ ] `cd backend && dotnet build FleetTelemetry.csproj` passes on the final branch state
- [ ] Open PR: `claude/sprint-04-editing-search-focused-view` → `main`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-04.md` → `docs/sprints/archive/sprint-04.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `docs/sprints/BACKLOG.md` (Sprint 05)
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
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-04-editing-search-focused-view
   - If not: git fetch origin main && git checkout -B claude/sprint-04-editing-search-focused-view origin/main
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
18. Commit: git commit -m "IIOT-S04-{TASK-ID}: <one-line summary>"

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
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **display_number** | New `vehicles` table column — operator-editable "fleet number," distinct from the immutable `id` primary key |
| **PATCH /api/vehicles/{id}** | New endpoint (BE-006) — edits `driverName`/`displayNumber` only; never renames `id` |
| **lastSeenAtUtc** | New per-vehicle timestamp (BE-007) — live mode: last ingest time; dummy mode: always "now" |
| **Focused view** | Default-on Sidebar behavior (UI-014) — shows ≤10 highest-priority vehicles unless "Show all" is toggled or a search query is active |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name |
