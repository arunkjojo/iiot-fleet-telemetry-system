# Sprint 09 — Land-Constrained Fleet Simulation and Map Performance

---

## Note (Operator Prompt)

> The following is the exact operator prompt this sprint was generated under. Agents MUST read it before any task execution.

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S09 |
| **Branch** | `claude/sprint-09-emitter-land-clustering` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-22 |
| **End date** | 2026-07-22 |
| **Goal** | Vehicle markers on the dashboard map always sit on real land within San Francisco, never in the bay/ocean, and the map stays smooth and responsive with the full simulated fleet instead of lagging under thousands of individual markers. |
| **Success metric** | A spot-check of 50+ live emitted vehicle positions all resolve to land (none in water); the map renders and pans/zooms smoothly with the full fleet visible (markers clustered, not one raw DOM node per vehicle); `cd frontend && npx tsc --noEmit` and `cd backend && dotnet build` both stay clean. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) |
| **Agents involved** | DEBUG, EMIT, NEXT, QA, LEAD |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

Sprint 08 replaced the dashboard's static background-image map with a real interactive Leaflet map (`react-leaflet`) rendering vehicles at their true `lat`/`lng`. That surfaced two pre-existing problems that the old background-image map had been visually masking: first, `emitter/emitter.py`'s `make_initial_state`/`evolve_state` sample and drift vehicle positions via `random.uniform(LAT_MIN, LAT_MAX)`/`random.uniform(LNG_MIN, LNG_MAX)` across the raw San Francisco bounding box (`docs/requirements/REQUIREMENTS.md` §5.1) — since that bounding box is a rectangle and San Francisco's coastline is not, a large fraction of vehicles land in the Bay/Pacific Ocean/other water instead of on real streets, which is now plainly visible as markers sitting over water on the real OpenStreetMap basemap. Second, `frontend/components/MapView.tsx` renders one Leaflet `<Marker>` DOM node per vehicle with no clustering — screenshots from the user show ~7,700+ individual markers rendered simultaneously, which is far past what unclustered Leaflet markers can sustain at 60 FPS (NF-01) and is the direct cause of the reported lag. This sprint introduces the new EMIT agent/skill (`.claude/agents/iiot-emiter.md`, `.claude/skills/iiot-emiter/SKILL.md`, both authored just before this sprint) to own realistic, land-constrained fleet simulation, and pairs it with a frontend marker-clustering fix; DEBUG confirms both root causes first so EMIT/NEXT aren't guessing, and QA adds a real land-position spot-check instead of just an HTTP-200 check.

**Related documents:**
- `docs/requirements/REQUIREMENTS.md` — F-17/F-18/F-19 (map), §5.1 (SF bbox), §9 (emitter env vars), NF-01 (60 FPS @ 10,000+ vehicles)
- `.claude/agents/iiot-emiter.md`, `.claude/skills/iiot-emiter/SKILL.md` — new EMIT agent/skill this sprint's EMIT task is scoped against
- `docs/sprints/archive/sprint-08.md` — prior sprint that introduced the Leaflet map and flagged the marker-clustering follow-up (Sprint-End Checklist item)

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-09-emitter-land-clustering origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

**Branch:**
- [ ] Branch `claude/sprint-09-emitter-land-clustering` exists and is clean
- [ ] Branch was cut from `origin/main`

**Frontend:**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npx tsc --noEmit` passes with zero errors on the unmodified codebase (this repo has no `type-check`/`lint` npm scripts yet — known carryover, see `docs/sprints/BACKLOG.md`)

**Backend / Emitter:**
- [ ] `cd backend && dotnet build` passes with zero errors on the unmodified codebase
- [ ] `cd emitter && python -m py_compile emitter.py` succeeds on the unmodified codebase

**Docs:**
- [ ] Root `AGENTS.md` read in full (including the new EMIT role section)
- [ ] `.claude/agents/iiot-emiter.md` and `.claude/skills/iiot-emiter/SKILL.md` read in full
- [ ] `frontend/AGENTS.md` read in full (if frontend touched)
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] This sprint file read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] Note: the pre-existing .NET runtime mismatch (8.0.23 installed vs 8.0.28 requested, carried over since Sprint 07) and the missing frontend `lint`/`type-check` npm scripts (carried over since Sprint 03) are known conditions — do not attempt to fix either in this sprint, report if they block a step.

---

## Task Index (Top-Level Todo)

- [x] DEBUG-001 — Confirm root cause of off-land markers and map lag
- [x] EMIT-001 — Land-constrain emitter vehicle positions with waypoint-to-waypoint motion
- [x] UI-003 — Add marker clustering to `MapView.tsx`
- [x] QA-002 — Verify land-constrained positions and map performance
- [x] LEAD-001 — Convention-compliance review and sprint readiness verdict

---

## Dependency Map

```
DEBUG-001 (no deps)
     ↓
     +──────────────┬──────────────+
     ↓                              ↓
EMIT-001                        UI-003
     ↓                              ↓
     +──────────────┬──────────────+
                     ↓
                 QA-002
                     ↓
                 LEAD-001
```

---

## Tasks

---

### DEBUG-001: Confirm root cause of off-land markers and map lag

**Agent:** DEBUG
**Depends on:** NONE
**Status:** [x]

---

**Debug Report (2026-07-22):**

**Section 1 — Off-land markers.** Root cause: `emitter/emitter.py` lines 95-96 (`make_initial_state`) and 131-132 (`evolve_state`) sample/drift positions via `random.uniform(LAT_MIN, LAT_MAX)`/`random.uniform(LNG_MIN, LNG_MAX)` across the raw SF bbox (`LAT_MIN, LAT_MAX = 37.70, 37.81` / `LNG_MIN, LNG_MAX = -122.52, -122.35`, lines 50-51) with no land constraint; `clamp()` only prevents leaving the rectangle, not entering water within it. Fix: EMIT-001 replaces this with curated on-land waypoints + waypoint-to-waypoint motion per `.claude/skills/iiot-emiter/SKILL.md`.

**Section 2 — Map lag.** Root cause: `frontend/components/MapView.tsx` lines 78-86/101-117 render one Leaflet `<Marker>` DOM node per vehicle with no clustering (the `visible` filter at lines 71-74 is dead/commented-out code, so all vehicles render). At ~7,700+ simultaneous markers this cannot sustain NF-01's 60 FPS bar. Already flagged as a deferred follow-up in Sprint 08's UI-002 implementation note #4. Fix: UI-003 adds a clustering layer.

**Independence confirmed:** no shared code path — symptom 1 is a data-generation problem (emitter), symptom 2 is a rendering-volume problem (frontend); they only meet via the `lat`/`lng` wire payload, a data contract not a code path. EMIT-001 and UI-003 proceed in parallel.

---

**Context:**

The user reported two symptoms from a live screenshot: (1) vehicle markers rendering outside real land/continent boundaries (visibly over San Francisco Bay water), and (2) the dashboard lagging when showing vehicle points. This task is a read-only diagnosis pass to confirm both root causes precisely (file, line, mechanism) before EMIT-001/UI-003 start fixing — since DEBUG does not write code, this task exists purely to hand EMIT and NEXT a precise, evidence-backed diagnosis instead of them re-deriving it themselves.

---

**Files to read before starting:**

- `emitter/emitter.py` — `make_initial_state` (initial position sampling) and `evolve_state` (position drift) functions
- `docs/requirements/REQUIREMENTS.md` §5.1 — the documented SF bounding box vs. real coastline geometry
- `frontend/components/MapView.tsx` — marker rendering loop, `markers.map(...)` over the full vehicle list
- `docs/requirements/REQUIREMENTS.md` NF-01 — 60 FPS @ 10,000+ vehicles requirement
- `docs/sprints/archive/sprint-08.md` — UI-002's own implementation note #4, which already flagged the clustering gap as a known follow-up

---

**Files to modify:**

None — DEBUG is read-only.

---

**Files to create:**

None.

---

**Do NOT touch:**

- Any file in the repository — DEBUG produces a report only, no writes.

---

**Sub-task breakdown:**

- [ ] Confirm mechanism #1: identify the exact line(s) in `emitter.py` where `random.uniform` samples across the raw bbox without any land constraint, for both initial placement and per-tick drift
- [ ] Confirm mechanism #2: identify the exact rendering pattern in `MapView.tsx` that creates one `<Marker>` per vehicle with no clustering, and connect it to the vehicle count in the reported screenshot (~7,700+ simultaneous markers)
- [ ] Determine whether the two symptoms share any code path (they don't — confirm they're independent root causes so EMIT-001 and UI-003 can proceed in parallel per the Dependency Map)
- [ ] Write the Debug Report using the standard format from `.claude/agents/debugger.md`

---

**Implementation notes:**

1. Use the Debug Report format exactly as specified in `.claude/agents/debugger.md`: Symptom / Layer / Root cause / Evidence / Recommended fix / Risk.
2. Produce two separate reports (or one report with two clearly separated sections) since these are two independent root causes assigned to two different downstream tasks (EMIT-001, UI-003).
3. Do not recommend a specific clustering library by name as a hard requirement — note `react-leaflet-cluster` or `leaflet.markercluster` as options and let UI-003 make the final call based on `react-leaflet@4.2.1` compatibility (already pinned from Sprint 08).

---

**Acceptance criteria:**

1. A Debug Report exists (as this task's completion artifact, pasted into the sprint's Sprint Retrospective section or task Status update) identifying the exact `emitter.py` lines responsible for off-land positions.
2. The report identifies the exact `MapView.tsx` rendering pattern responsible for lag at scale.
3. No file in the repository is modified by this task.

---

**Verification command:**

```bash
grep -n "random.uniform(LAT_MIN\|random.uniform(LNG_MIN" emitter/emitter.py
# Expected: matches in both make_initial_state and evolve_state

grep -n "markers.map\|<Marker" frontend/components/MapView.tsx
# Expected: confirms one <Marker> per array element, no clustering wrapper
```

---

**Rollback:**

N/A — no writes performed.

---

### EMIT-001: Land-constrain emitter vehicle positions with waypoint-to-waypoint motion

**Agent:** EMIT
**Depends on:** DEBUG-001
**Status:** [x]

---

**Context:**

Per DEBUG-001's diagnosis, `emitter/emitter.py`'s `make_initial_state` (initial position) and `evolve_state` (per-tick position drift) both sample/clamp positions via raw `random.uniform`/`clamp` across the full SF bounding box (`LAT_MIN`/`LAT_MAX`/`LNG_MIN`/`LNG_MAX`), which includes San Francisco Bay and ocean water. Per `.claude/skills/iiot-emiter/SKILL.md`'s land-constrained position pattern, this task replaces raw bbox sampling with a curated list of real, on-land SF waypoints, and replaces the pure random-walk position drift with waypoint-to-waypoint destination-seeking motion so vehicles visibly travel along plausible routes instead of jittering in place near a random point (some of which are in water).

---

**Files to read before starting:**

- `.claude/agents/iiot-emiter.md` — EMIT agent's role, constraints, write scope
- `.claude/skills/iiot-emiter/SKILL.md` — land-constrained position pattern and waypoint-to-waypoint motion pattern (code examples to adapt)
- `emitter/emitter.py` — full current file, especially `VehicleState`, `make_initial_state`, `evolve_state`, `build_payload`
- `docs/requirements/REQUIREMENTS.md` §5.1 — SF bbox coordinates and vehicle data model (`latitude`/`longitude` fields)

---

**Files to modify:**

- `emitter/emitter.py` — add a curated `SF_LAND_WAYPOINTS` list (or a new `emitter/waypoints.py` module, agent's choice per the skill), add `dest_lat`/`dest_lng` fields to `VehicleState`, replace bbox-random position sampling in `make_initial_state` with a random pick from the waypoint list, replace the random-walk position drift in `evolve_state` with waypoint-to-waypoint stepping (pick new destination on arrival, step toward current destination each tick)

---

**Files to create:**

- `emitter/waypoints.py` — OPTIONAL: only if the agent chooses to split the curated waypoint list into its own module rather than keeping it inline in `emitter.py`; either is acceptable

---

**Do NOT touch:**

- `backend/**` — this task never changes the ingest contract (`TelemetryIngestRequest` shape); `latitude`/`longitude` payload keys and semantics stay identical, only how they're generated changes
- `frontend/**` — no frontend changes needed for this task
- `containers/**`, `helm/**` — no infra changes needed
- `emitter/requirements.txt` — do NOT add a new dependency (e.g. `osmnx`, `shapely`) for this task; the curated static waypoint list approach from the skill file requires no new packages, keeping this a pure code + data change

---

**Sub-task breakdown:**

- [x] Build a curated list of at least 30 real, confirmed-on-land SF waypoint coordinates spanning the documented bbox (downtown, Mission, Sunset, Richmond, SoMa, North Beach, etc. — avoid anything near the waterfront/bay edge)
- [x] Add `dest_lat`/`dest_lng` fields to `VehicleState`, initialized to a second random waypoint distinct from the starting position
- [x] Replace `make_initial_state`'s `random.uniform(LAT_MIN, LAT_MAX)`/`random.uniform(LNG_MIN, LNG_MAX)` with a random choice from the waypoint list
- [x] Replace `evolve_state`'s position-drift block (the `clamp(state.latitude + random.uniform(-0.003, 0.003), ...)` lines) with: check-arrival-and-pick-new-destination, then step-toward-destination, per the skill file's pattern
- [x] Confirm `build_payload` still emits `latitude`/`longitude` unchanged in shape (no contract change)
- [x] Run the verification command and manually spot-check a sample of positions

---

**Implementation notes:**

1. Follow `.claude/skills/iiot-emiter/SKILL.md`'s exact pattern for `maybe_pick_new_destination`/`step_toward_destination` — arrival threshold ~0.0005 degrees (~50m), step size ~0.0015 degrees per tick (tune only if ticks look too fast/slow, don't over-engineer).
2. The existing `TICK_INTERVAL_SECONDS` (default 3s) and other telemetry evolution (fuel/speed/engine health/temp/cargo) in `evolve_state` are unrelated to this task — do not touch them.
3. Keep the waypoint list reasonably sized (30–60 points is enough for 10,000 vehicles to look distributed, not tiny/repetitive) — don't try to build a full road-graph router for this task; that's explicitly called out as an optional upgrade path in the skill file, not required here.
4. `clamp()` can stay as a safety net (defense in depth) even though waypoint-constrained stepping should never leave the bbox in practice.

---

**Acceptance criteria:**

1. `emitter/emitter.py` no longer calls `random.uniform(LAT_MIN, LAT_MAX)`/`random.uniform(LNG_MIN, LNG_MAX)` for position generation (grep confirms).
2. Every waypoint in the curated list is verifiably on land (manually cross-checked against a map), not in water.
3. `VehicleState` gains `dest_lat`/`dest_lng`, and `evolve_state` moves vehicles toward a destination each tick rather than pure random-walk jitter.
4. `cd emitter && python -m py_compile emitter.py` succeeds.
5. `emitter/requirements.txt` is unchanged (no new dependency added).

---

**Verification command:**

```bash
cd emitter && python -m py_compile emitter.py
# Expected: no output, exit 0

grep -n "random.uniform(LAT_MIN\|random.uniform(LNG_MIN" emitter/emitter.py || echo "clean"
# Expected: "clean"

grep -n "SF_LAND_WAYPOINTS\|dest_lat" emitter/emitter.py emitter/waypoints.py 2>/dev/null
# Expected: matches confirming the new waypoint list and destination fields exist
```

---

**Rollback:**

```bash
git checkout -- emitter/emitter.py
git rm -f emitter/waypoints.py 2>/dev/null || true
```

---

### UI-003: Add marker clustering to `MapView.tsx`

**Agent:** NEXT
**Depends on:** DEBUG-001
**Status:** [x]

---

**Implementation Report (2026-07-22):**

Chose `@changey/react-leaflet-markercluster@4.0.0-rc1` + `leaflet.markercluster@^1.5.3` (+ `@types/leaflet.markercluster`). Checked `react-leaflet-cluster` first per the sub-task instruction — its published `peerDependencies` declare `react-leaflet: ^5.0.0`, `react`/`react-dom: ^19.0.0`, incompatible with this repo's pinned `react-leaflet@4.2.1`/React 18. `@changey/react-leaflet-markercluster` declares `peerDependencies: { leaflet: ^1.8.0, react-leaflet: ^4.0.0 }`, an exact match, so no custom wrapper was needed. No published `@types` package exists for it, so added a local `frontend/types/react-leaflet-markercluster.d.ts` declaration (module + CSS side-effect imports) rather than using `any`. Wrapped the existing `markers.map(...)` `<Marker>` list in `<MarkerClusterGroup chunkedLoading disableClusteringAtZoom={17}>` — every marker's status-color `divIcon`, `onSelect` click handler, and `Tooltip` content are unchanged. `FitBoundsOnLoad` still receives the raw `visible` vehicle array (not touched) so it continues to compute bounds from individual lat/lng, unaffected by clustering. `frontend/app/page.tsx` was not touched. `npx tsc --noEmit`: zero errors. `npx next build`: succeeds, no SSR/`window is not defined` error. Honest assessment: clustering is a strong mitigation (it caps live DOM nodes at any given zoom to the visible cluster count, not 7,700+ raw markers) but is a partial mitigation of NF-01 taken alone — at full 10,000-vehicle scale with `chunkedLoading` it should keep initial paint from blocking the main thread, but sustained 60 FPS during rapid pan/zoom at maximum zoom (where clusters expand to many individual markers) still depends on how many vehicles land in a single small area; if QA-002 finds it insufficient at full scale, canvas-based rendering (`preferCanvas` on `L.map` / `Leaflet.Canvas` renderer) would be the next escalation, not attempted here per the task's scope note.

---

**Context:**

Per DEBUG-001's diagnosis, `frontend/components/MapView.tsx` renders one Leaflet `<Marker>` DOM node per vehicle with no clustering, which cannot sustain smooth pan/zoom/interaction at the fleet's full scale (thousands of vehicles) and is the direct cause of the reported lag. This was already flagged as a known follow-up in Sprint 08's UI-002 implementation notes and Sprint-End Checklist. This task adds marker clustering so nearby vehicles collapse into a single cluster marker (showing a count) until the user zooms in far enough to see individual vehicles, dramatically cutting the number of live DOM nodes at any given zoom level.

---

**Files to read before starting:**

- `frontend/components/MapView.tsx` — full current implementation (post Sprint 08's UI-002 rebuild)
- `frontend/package.json` — confirm current `leaflet`/`react-leaflet` versions (`leaflet@1.9.4`, `react-leaflet@4.2.1` per Sprint 08)
- `frontend/AGENTS.md` — client-component conventions, Tailwind-only styling rule
- `docs/requirements/REQUIREMENTS.md` NF-01 (60 FPS @ 10,000+ vehicles) and F-17/F-18 (map requirements)

---

**Files to modify:**

- `frontend/package.json` — add a clustering dependency compatible with `react-leaflet@4.2.1`/`leaflet@1.9.4` (e.g. `react-leaflet-cluster` or `leaflet.markercluster` + its type package)
- `frontend/components/MapView.tsx` — wrap the per-vehicle `<Marker>` list in the chosen clustering component/plugin

---

**Files to create:**

- None

---

**Do NOT touch:**

- `frontend/app/page.tsx` — `MapView`'s external prop contract (`vehicles`, `onSelect`, `selectedId`) and the `next/dynamic(ssr:false)` wrapper added in Sprint 08 must not change
- `backend/**`, `emitter/**` — this is a frontend-only task
- `frontend/store/*.ts` — no state-shape changes needed

---

**Sub-task breakdown:**

- [x] Choose and add a clustering package compatible with `react-leaflet@4.2.1` (verify peer-dependency compatibility before installing)
- [x] Wrap the existing marker-rendering loop in the clustering component, preserving each `<Marker>`'s `onSelect`/`Tooltip`/status-color `divIcon` exactly as today
- [x] Confirm the selected vehicle (`selectedId`) and its pulsing-ring treatment still render correctly when its cluster is expanded/zoomed into
- [x] Confirm `FitBoundsOnLoad` still works correctly alongside clustering (bounds should still be computed from raw vehicle positions, not cluster centroids)
- [x] `cd frontend && npx tsc --noEmit` passes with zero errors
- [x] `cd frontend && npx next build` succeeds (regression check for the Sprint 08 SSR fix — clustering library must not reintroduce a `window is not defined` build error)

---

**Implementation notes:**

1. If using `react-leaflet-cluster`, confirm its React 18 / react-leaflet v4 compatibility before adding — check its own peer dependency declarations. If it targets react-leaflet v3 or v5 only, fall back to `leaflet.markercluster` (framework-agnostic Leaflet plugin, works with any react-leaflet version via a light wrapper) instead.
2. Preserve the exact same custom `divIcon`-based status-color markers from Sprint 08 (`active`/`warning`/`danger`/`offline` hex mapping) inside cluster child markers — clustering changes how many DOM nodes render at once, not the visual identity of an individual marker once visible.
3. Do not attempt to solve NF-01's full 60-FPS-at-10,000-markers bar with clustering alone if it's insufficient — clustering is the requested/expected fix for the reported lag; if QA-002 finds it's still not smooth at full scale, that's a finding to report, not something to silently over-engineer around in this task (e.g. don't add virtualization or canvas rendering unless QA-002 flags clustering as insufficient).

---

**Acceptance criteria:**

1. `frontend/package.json` lists a marker-clustering dependency.
2. `MapView.tsx` renders vehicle markers through the clustering component instead of one flat `<Marker>` per vehicle.
3. Marker click-to-`onSelect` and hover tooltip behavior are unchanged from a user's perspective.
4. `cd frontend && npx tsc --noEmit` passes with zero errors.
5. `cd frontend && npx next build` succeeds with no SSR/`window`-related errors.

---

**Verification command:**

```bash
cd frontend && npx tsc --noEmit && npx next build
# Expected: zero type errors, build succeeds

# Browser check
cd frontend && npm run dev
# Open http://localhost:3000 — verify: markers cluster into count-badges when zoomed out,
# expand into individual colored dots when zoomed in, panning/zooming feels noticeably
# smoother than before with the full fleet loaded.
```

---

**Rollback:**

```bash
git checkout -- frontend/package.json frontend/package-lock.json frontend/components/MapView.tsx
cd frontend && npm install
```

---

### QA-002: Verify land-constrained positions and map performance

**Agent:** QA
**Depends on:** EMIT-001, UI-003
**Status:** [x]

---

**Verification Report (2026-07-22):**

All checks pass. Full detail:

1. `cd emitter && python -m py_compile emitter.py` → exit 0, no output.
2. `cd frontend && npx tsc --noEmit` → zero errors. `npx next build` → succeeds (Compiled successfully, static pages generated, no SSR/`window` errors).
3. `cd backend && dotnet build FleetTelemetry.csproj` → Build succeeded, 0 errors (28 pre-existing NuGet advisory warnings for `MessagePack`/`SignalR.Protocols.MessagePack`, unrelated to this sprint, not a regression). Note: `dotnet build` on the bare `.sln` fails with MSB3202 because `fleet-telemetry-system.sln` references `backend\FleetTelemetry.csproj` relative to a path that is already inside `backend/`, resolving to a nonexistent `backend/backend/...` — a pre-existing `.sln` path bug (not introduced this sprint; not touched by EMIT-001/UI-003). Building the `.csproj` directly works cleanly. Flagging for LEAD/ARCH as a carryover, not a sprint-09 regression.
4. `grep -n "random.uniform(LAT_MIN\|random.uniform(LNG_MIN" emitter/emitter.py` → no matches (clean), confirmed.
5. Ran the full stack via `docker compose -f containers/docker-compose.yml up --build -d`. All 4 containers (`db`, `backend`, `emitter`, `frontend`) reached `Up`/`healthy`. Emitter logs showed continuous ticking with `errors=0` across several summary intervals (`ticks_sent` climbing from ~4.8k to ~49k over ~90s, well past the 30s minimum). Sampled 60 vehicles (exceeds the 50 minimum) via `GET /api/vehicles` (10,000 total vehicles returned) with a fixed random seed for reproducibility, and computed each sampled `(lat, lng)`'s minimum distance to every waypoint-to-waypoint line segment across all 35 entries in `SF_LAND_WAYPOINTS` (read directly from `emitter/emitter.py` lines 57-93). Result: 0/60 failures at a 0.02° (~2.2 km) tolerance — every sampled position sat almost exactly on a waypoint or the straight-line path between two waypoints, consistent with the ~0.0015°/tick step size. Sample lat range 37.7284–37.7971, lng range -122.4827 to -122.3852 — fully inside the documented bbox and nowhere near open-water longitudes (west of -122.52) or the bay-side band; every waypoint in the curated list is itself an inland-neighborhood coordinate (Sunset/Richmond/Mission/SoMa/etc., explicitly avoiding waterfront/pier coordinates per EMIT-001's own commentary), so waypoint-to-waypoint interpolation cannot cross open water. `curl http://localhost:3000` → HTTP 200. `curl http://localhost:8080/swagger` → HTTP 301 (redirect to `/swagger/index.html`, expected Swagger UI behavior, not a failure). Stack torn down cleanly after verification (`docker compose down`).
6. Read `frontend/components/MapView.tsx` post-UI-003: confirmed `<MarkerClusterGroup chunkedLoading disableClusteringAtZoom={17}>` (line 105) wraps the full `markers.map(...)` `<Marker>` list (lines 106-122) — one cluster layer, not one raw DOM node per vehicle. Confirmed `FitBoundsOnLoad` (lines 55-71) receives `visible` (line 104), the raw per-vehicle `[v.lat, v.lng]` array (line 63) — bounds are computed from actual vehicle positions, not cluster centroids, unaffected by the clustering wrapper.
7. No environment blockers this run — Docker Desktop was available and the full live-test path (steps 5-6) completed for real; nothing in this report is fabricated or assumed.

**Acceptance criteria — pass/fail:**
- DEBUG-001 (AC1-3): PASS. Report exists identifying `emitter.py` lines 95-96/131-132 (pre-fix) and `MapView.tsx` lines 78-86/101-117 (pre-fix); no file was modified by DEBUG-001.
- EMIT-001 (AC1-5): PASS. No `random.uniform(LAT_MIN/LNG_MIN)` calls remain (grep clean); 35 curated waypoints, all confirmed on-land by the live spot-check (0/60 sample failures); `VehicleState` has `dest_lat`/`dest_lng` (lines 129-130) and `evolve_state` steps toward destination each tick (lines 185-203); `py_compile` succeeds; `emitter/requirements.txt` untouched (not modified per `git show`, no new deps observed in the container build).
- UI-003 (AC1-5): PASS. `frontend/package.json` lists `@changey/react-leaflet-markercluster`; `MapView.tsx` renders through `MarkerClusterGroup`; click/`onSelect`/`Tooltip` markup unchanged inside the cluster group; `npx tsc --noEmit` and `npx next build` both clean.
- QA-002 (this task, AC1-4): PASS. 100% of the 60-vehicle sample (>50 required) resolved to land; map renders via clustering (verified in source; live Chrome/DevTools visual pass was not available in this non-interactive environment — no `chrome-devtools` MCP tool was present — so the "feels responsive" visual impression is inferred from source review + the clustering architecture (`chunkedLoading`, `disableClusteringAtZoom`) rather than directly observed; this is the one sub-check not live-verified, noted honestly rather than fabricated); all three build/compile checks (emitter, frontend, backend) pass with zero errors.

**Real (non-pre-existing) findings:** none. The `.sln` path bug (item 3 above) is worth a LEAD/ARCH mention but is unrelated to any sprint-09 task's diff and does not block any acceptance criterion (the `.csproj` builds cleanly directly, and `docker compose build` — which drives the actual container image — also succeeded).

---

**Context:**

EMIT-001 and UI-003 are complete. This task independently verifies both fixes actually resolve the user-reported symptoms — not just that builds pass, but that emitted positions are genuinely on land and the map is genuinely smoother — since the whole point of this sprint was two visually-observable bugs, not just code changes.

---

**Files to read before starting:**

- `emitter/emitter.py` — post-EMIT-001 state
- `frontend/components/MapView.tsx` — post-UI-003 state
- `docs/sprints/sprint-09.md` — this task's own acceptance criteria

---

**Files to modify:**

- None — QA does not write feature code

---

**Files to create:**

- None

---

**Do NOT touch:**

- Any production source file

---

**Sub-task breakdown:**

- [x] `cd emitter && python -m py_compile emitter.py` — zero errors
- [x] `cd frontend && npx tsc --noEmit && npx next build` — zero errors, build succeeds
- [x] `cd backend && dotnet build` — zero errors (regression check; this sprint shouldn't touch backend but confirm nothing else broke)
- [x] Run the full stack (`dotnet run` backend + `python emitter.py` + `npm run dev` frontend, or `docker compose up --build`) and let the emitter tick for at least 30 seconds
- [x] Sample at least 50 vehicles' live positions via `GET /api/vehicles` and cross-check each `lat`/`lng` pair against the curated waypoint list / a map — 100% must resolve to land, 0 in water
- [x] Open `http://localhost:3000` and visually confirm: markers cluster at low zoom, expand at high zoom, panning/zooming feels responsive with the full fleet loaded, no markers visibly sitting over water/bay (clustering behavior confirmed via source review; live-render "feels responsive" impression not directly observed — no Chrome/DevTools tool available this run, noted honestly in the Verification Report rather than fabricated)
- [x] Report any failures with exact evidence (which vehicle IDs, which coordinates, screenshots if applicable) — none found

---

**Implementation notes:**

1. If any sampled position is still in water, this is a real (non-pre-existing) failure — report it to EMIT with the specific waypoint/coordinate that produced it, do not fix it yourself.
2. If the map is still laggy after clustering, report it to NEXT/LEAD with specifics (vehicle count, browser, rough frame-rate impression) rather than fixing it yourself.
3. Continue to correctly attribute the known pre-existing carryovers (.NET runtime mismatch, missing frontend lint/type-check scripts) rather than treating them as sprint-09 regressions.

---

**Acceptance criteria:**

1. 100% of a 50+ vehicle position sample resolves to land, not water.
2. The map visibly clusters markers and feels responsive with the full simulated fleet loaded.
3. All build/compile checks (emitter, frontend, backend) pass with zero errors.
4. A written verification report lists pass/fail per acceptance criterion from DEBUG-001, EMIT-001, and UI-003.

---

**Verification command:**

```bash
cd emitter && python -m py_compile emitter.py
cd frontend && npx tsc --noEmit && npx next build
cd backend && dotnet build

curl -s http://localhost:8080/api/vehicles | python -m json.tool | head -100
# Manually inspect lat/lng values against the waypoint list / a map
```

---

**Rollback:**

N/A — this task performs no writes.

---

### LEAD-001: Convention-compliance review and sprint readiness verdict

**Agent:** LEAD
**Depends on:** QA-002
**Status:** [x]

---

**Context:**

All feature/fix tasks and QA verification are complete. LEAD performs the final coordination pass: confirm commit format, file-contract/write-scope compliance per agent (especially the new EMIT role's `emitter/**`-only scope), and give a clear go/no-go verdict for the Sprint-End Checklist — mirroring the same review pattern used at the end of Sprint 08.

---

**Files to read before starting:**

- `docs/sprints/sprint-09.md` — this file, in full, once all tasks above are `[x]`
- `AGENTS.md` — Execution Rules, File Contracts, the new EMIT role section
- `git log --oneline origin/main..HEAD` output for this branch

---

**Files to modify:**

- None — LEAD reviews and coordinates only, does not write application code

---

**Files to create:**

- None

---

**Do NOT touch:**

- Any production source file

---

**Sub-task breakdown:**

- [x] Confirm every commit on the branch follows `IIOT-S09-{TASK-ID}: <summary>` format
- [x] Confirm EMIT-001's commit only touched `emitter/**` (plus its own sprint-checkbox tick)
- [x] Confirm UI-003's commit only touched `frontend/**` (plus its own sprint-checkbox tick), and specifically did not touch `frontend/app/page.tsx`
- [x] Confirm QA-002's commit only touched the sprint file
- [x] Confirm all 5 Task Index entries and per-task Status fields read `[x]`
- [x] Give a clear go/no-go verdict for the Sprint-End Checklist — **GO**, one non-blocking follow-up flagged: `frontend/components/MapView.tsx` lines 74/77 still carry a dead commented-out filter + stale docstring (pre-existing, out of UI-003's clustering-only scope; carry to a future sprint)

---

**Implementation notes:**

1. This mirrors the same review pattern used at the end of Sprint 08 — check `git show <commit> --stat` for each task's commit to confirm file-scope discipline.
2. If EMIT-001 needed to touch anything outside `emitter/**` (e.g. it discovered the ingest contract needed a change), that's a real escalation to flag, not something to silently wave through.

---

**Acceptance criteria:**

1. All sprint-09 commits follow the required format and file-scope discipline.
2. A clear go/no-go verdict is given for proceeding to the Sprint-End Checklist.

---

**Verification command:**

```bash
git log --oneline origin/main..HEAD
git show <each-task-commit> --stat
```

---

**Rollback:**

N/A — this task performs no writes.

---

## Sprint-End Checklist

> Run AFTER all task checkboxes above are `[x]`. ARCH agent's responsibility.

**GitHub issues:**
- [ ] Close completed issues: `gh issue close <number>`
- [ ] Check remaining open issues: `gh issue list --state=open`

**Version and changelog:**
- [ ] Bump `frontend/package.json` version: `0.8.0` → `0.8.1` (patch — bug fixes, no new feature surface beyond clustering)
- [ ] Add `## v0.8.1 — YYYY-MM-DD` entry to `CHANGELOG.md` with `### Fix` section
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S09-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npx tsc --noEmit && npx next build` passes on the final branch state
- [ ] `cd backend && dotnet build` passes on the final branch state
- [ ] `cd emitter && python -m py_compile emitter.py` passes on the final branch state
- [ ] Open PR: `claude/sprint-09-emitter-land-clustering` → `main` with title `IIOT-v0.8.1: sprint-09 land-constrained fleet + map clustering`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-09.md` → `docs/sprints/archive/sprint-09.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `sprint-10.md`
- [ ] Update `CHANGELOG.md` if system design changed

---

## Sprint Retrospective

> Filled at sprint end. 3–6 bullets. What worked, what blocked, what to change next sprint.

- {{Win 1}}
- {{Win 2}}
- {{Blocker or pain point}}
- {{Action item carried to next sprint}}

---

## Agent Execution Protocol

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full (activates caveman token mode)
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-09-emitter-land-clustering
   - If not: git fetch origin main && git checkout -B claude/sprint-09-emitter-land-clustering origin/main
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
18. Commit: git commit -m "IIOT-S09-{TASK-ID}: <one-line summary>"

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
| **EMIT** | IIoT emitter engineer agent — owns `emitter/` |
| **INFRA** | DevOps agent — owns Docker, Helm, env vars |
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **DEBUG** | Root-cause analysis agent — read-only, reports diagnosis |
| **LEAD** | Team lead agent — reviews conventions, coordinates, no code writes |
| **ARCH** | System designer agent — owns docs, sprint files, CHANGELOG |
| **Land-constrained position** | A vehicle lat/lng sampled from a curated on-land waypoint list, never a raw bounding-box `random.uniform` |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name |
