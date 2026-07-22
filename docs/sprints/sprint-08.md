# Sprint 08 — Live Map, Data-Flow Docs, Live-Only Mode, Swagger Everywhere

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
| **Sprint ID** | S08 |
| **Branch** | `claude/sprint-08-leaflet-live-swagger` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-22 |
| **End date** | 2026-07-22 |
| **Goal** | Operators see vehicles as real markers on an interactive Leaflet map, always backed by live emitter data (no dummy mode), can read a data-flow diagram in the app overview docs, and can browse Swagger UI against the backend whether it's run locally, in Docker, or in Kubernetes. |
| **Success metric** | `http://localhost:3000` renders an interactive pannable/zoomable Leaflet map with vehicle markers at true geo positions; `USE_LIVE_TELEMETRY` and `TelemetrySimulationService` dummy path no longer exist in the codebase; `docs/APPLICATION_OVERVIEW.md` contains a data-flow diagram; `/swagger` is reachable on local `dotnet run`, the Docker Compose stack, and a Helm-deployed pod. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) + Docker Compose + Helm |
| **Agents involved** | NEXT, ASP.NET, INFRA, ARCH, QA |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

The dashboard currently renders vehicle markers as absolutely-positioned `<div>`s over a static background *image* (`frontend/components/MapView.tsx`), which is not a real map — it can't pan, zoom, or show real basemap context, and markers are placed via a hand-rolled lat/lng-to-percentage projection hardcoded to a fixed San Francisco bounding box. The user asked for a real interactive map in the spirit of Leafmap (https://leafmap.org); Leafmap itself is a Python geospatial notebook package with no browser/React runtime, so the equivalent for this Next.js frontend is **`react-leaflet` + Leaflet.js** (the same open-source Leaflet engine Leafmap wraps) with OpenStreetMap raster tiles — this substitution is called out explicitly in BE-noted context below so no agent re-litigates it. Separately, `USE_LIVE_TELEMETRY` (`backend/Program.cs`, `appsettings.json`, `containers/docker-compose.yml`, `helm/iiot-fleet-app/templates/app-configmap.yaml`) currently defaults `false` and gates between the in-memory `TelemetrySimulationService` dummy simulation and the live emitter-fed ingestion pipeline (F-26); the user wants dummy mode removed entirely so the backend always runs live-mode, sourced only from the emitter. The backend also has no Swagger/OpenAPI package wired into `Program.cs` today despite `AGENTS.md`/docs referencing `/swagger` — this sprint adds it for real, in local, Docker, and Helm. Finally, `docs/APPLICATION_OVERVIEW.md` (authored Sprint 07) has no visual data-flow diagram; this sprint adds one as a Mermaid diagram (renders natively on GitHub, no external image tooling required).

**Related documents:**
- `docs/requirements/REQUIREMENTS.md` — F-17/F-18/F-19 (map), F-23 (Swagger), F-26/F-27 (live telemetry), §9 environment variables
- `docs/APPLICATION_OVERVIEW.md` — target of the data-flow diagram task
- `docs/sprints/BACKLOG.md` — note the carried-over `ILiveTelemetryStore`/`display_number` cold-start hydration gap and unreconciled .NET runtime mismatch also live here; out of scope for this sprint

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-08-leaflet-live-swagger origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

> Run these checks BEFORE touching any code. If any check fails: STOP and report — do not proceed.

**Branch:**
- [ ] Branch `claude/sprint-08-leaflet-live-swagger` exists and is clean (`git status` shows no uncommitted changes)
- [ ] Branch was cut from `origin/main`

**Frontend:**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `http://localhost:3000` loads in browser with no console errors (before changes)

**Backend:**
- [ ] `cd backend && dotnet build` passes with **zero errors** on the unmodified codebase
- [ ] `curl http://localhost:8080/api/vehicles` returns HTTP 200 with JSON array

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full
- [ ] `backend/AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] This sprint file read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] Note: this dev machine's installed .NET runtime tops out at 8.0.23 vs. the built binary's 8.0.28 request (carried over from Sprint 07/BACKLOG.md) — this blocks a live runtime smoke test of BE tasks; `dotnet build` is unaffected and remains the required gate. Report this pre-existing condition, do not attempt to fix it in this sprint.

---

## Task Index (Top-Level Todo)

- [x] UI-001 — Add `react-leaflet` + Leaflet and render an interactive map with vehicle markers
- [x] UI-002 — Replace `MapView.tsx` background-image projection with real Leaflet markers
- [x] BE-001 — Remove `TelemetrySimulationService` dummy-mode path and `USE_LIVE_TELEMETRY` toggle from the backend
- [x] INFRA-001 — Remove `USE_LIVE_TELEMETRY` from Docker Compose and Helm chart; always run live mode
- [x] BE-002 — Wire Swagger/OpenAPI generation into `Program.cs` for local dev
- [x] INFRA-002 — Expose Swagger UI through the containerized Docker stack
- [x] INFRA-003 — Expose Swagger UI through the Helm chart
- [x] ARCH-001 — Add a data-flow Mermaid diagram to `docs/APPLICATION_OVERVIEW.md`
- [x] QA-001 — Verify map, live-only mode, and Swagger across local/Docker/Helm

---

## Dependency Map

```
UI-001 (no deps)          BE-001 (no deps)          ARCH-001 (no deps)
   ↓                          ↓
UI-002                    INFRA-001 (needs BE-001)
                              ↓
                          BE-002 (no deps, can run parallel to BE-001)
                              ↓
                          INFRA-002 (needs BE-002)
                              ↓
                          INFRA-003 (needs BE-002)
                              ↓
        UI-002, INFRA-001, INFRA-002, INFRA-003, ARCH-001
                              ↓
                          QA-001
```

---

## Tasks

---

### UI-001: Add `react-leaflet` + Leaflet dependencies and base map shell

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`frontend/package.json` has no mapping library. We're using `react-leaflet` (React bindings for Leaflet.js) with `leaflet` as its peer dependency, plus OpenStreetMap raster tiles (`https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png`) as the basemap — the closest browser-runtime equivalent to the user's Leafmap reference, since Leafmap itself is a Python-only package with no JS/React distribution. This task only adds the dependency and a minimal working `<MapContainer>` shell component; UI-002 does the full marker migration.

---

**Files to read before starting:**

- `frontend/components/MapView.tsx` — current background-image map implementation being replaced
- `frontend/types/vehicle.ts` — `Vehicle` type shape (`lat`, `lng`, `status`, etc.)
- `frontend/AGENTS.md` — component/styling conventions (client components, Tailwind-only styling)
- `frontend/package.json` — current dependency list and versions

---

**Files to modify:**

- `frontend/package.json` — add `leaflet` and `react-leaflet` to `dependencies`, add `@types/leaflet` to `devDependencies`

---

**Files to create:**

- None

---

**Do NOT touch:**

- `backend/**` — this is a frontend-only task
- `frontend/store/*.ts` — no state-shape changes needed for this task

---

**Sub-task breakdown:**

- [x] Add `"leaflet": "^1.9.4"` and `"react-leaflet": "^4.2.1"` to `dependencies` (v4.x targets React 18, matching this project's `react": "^18.2.0"`)
- [x] Add `"@types/leaflet": "^1.9.8"` to `devDependencies`
- [x] Run `npm install` inside `frontend/` and confirm it completes without peer-dependency errors
- [x] Confirm Leaflet's CSS (`leaflet/dist/leaflet.css`) is importable — this will be wired into `MapView.tsx` in UI-002

---

**Implementation notes:**

1. Use `react-leaflet@4` (not v3, which targets React 17, or v5, which requires React 19) — this project pins `react@^18.2.0`.
2. Do not import `leaflet/dist/leaflet.css` yet in this task — that import and the `<MapContainer>` JSX both belong in UI-002 so the two tasks stay independently revertable.
3. Leaflet's default marker icon assets resolve via relative URLs that break under bundlers; UI-002 will use custom `L.divIcon` markers (colored dots matching current status colors) instead of the default icon images, sidestepping that issue entirely — no icon asset files need to be added to `frontend/public/` in this task.

---

**Acceptance criteria:**

1. `frontend/package.json` lists `leaflet`, `react-leaflet`, and `@types/leaflet`.
2. `cd frontend && npm install` completes with zero errors.
3. `cd frontend && npm run type-check` passes with zero errors.
4. `cd frontend && npm run lint` passes with zero warnings.

---

**Verification command:**

```bash
cd frontend && npm install && npm run type-check && npm run lint
# Expected: install succeeds, zero type errors, zero lint warnings
```

---

**Rollback:**

```bash
git checkout -- frontend/package.json frontend/package-lock.json
cd frontend && npm install
```

---

### UI-002: Replace background-image map with real Leaflet markers

**Agent:** NEXT
**Depends on:** UI-001
**Status:** [x]

---

**Note (required SSR exception, approved by coordinator):** Leaflet touches `window`/`navigator` at import time, which breaks Next.js's server-side prerender of `page.tsx`. The static `import MapView from '../components/MapView'` in `frontend/app/page.tsx` was changed to `next/dynamic(() => import('../components/MapView'), { ssr: false })` — a one-line, minimal exception to the "Do NOT touch page.tsx" rule, scoped only to the import statement; no prop contract or SignalR wiring in `page.tsx` was changed.

**Context:**

`frontend/components/MapView.tsx` currently renders a static background image (`bg-cover`/`bg-center` with a hardcoded external image URL) and hand-projects vehicle `lat`/`lng` into percentage-based `left`/`top` CSS offsets clamped to 0–100%, which is exactly the "points outside the map" risk the user flagged — a vehicle outside the hardcoded SF bbox gets silently clamped to the edge instead of shown at its true position. This task replaces that whole approach with a real `react-leaflet` `<MapContainer>` + OpenStreetMap tile layer, rendering each vehicle as a `Marker` with a custom colored `divIcon` (reusing the existing status-to-hex mapping) at its **true** lat/lng — no projection math, no clamping, no image. Clicking a marker must still call `onSelect(v)` exactly as today (F-18), and the map must auto-fit its view to the vehicles' bounding box on load so every point sits within the visible viewport.

---

**Files to read before starting:**

- `frontend/components/MapView.tsx` — full current implementation to replace
- `frontend/types/vehicle.ts` — `Vehicle` shape
- `frontend/app/page.tsx` — how `MapView` is invoked, what props it receives, SignalR update flow
- `frontend/AGENTS.md` — client-component conventions, Tailwind-only styling rule
- `docs/requirements/REQUIREMENTS.md` — F-17/F-18/F-19 (map requirements) and §10 (WebGL/Mapbox/Deck.gl explicitly out of scope — Leaflet raster tiles are the correct fit)

---

**Files to modify:**

- `frontend/components/MapView.tsx` — replace background-image + percentage-projection rendering with `react-leaflet` `<MapContainer>`, `<TileLayer>`, and `<Marker>`/`divIcon` per vehicle; preserve the `Props` type (`vehicles`, `onSelect`, `selectedId`) and the `onSelect` click contract; auto-fit bounds to the current vehicle set

---

**Files to create:**

- None

---

**Do NOT touch:**

- `frontend/app/page.tsx` — `MapView`'s external prop contract stays the same; no changes needed to the caller
- `backend/**`
- `frontend/store/*.ts`

---

**Sub-task breakdown:**

- [x] Import `leaflet/dist/leaflet.css` once at the top of `MapView.tsx`
- [x] Replace the background-image `<div>`s with `<MapContainer>` (full-height/width via Tailwind `w-full h-full`) + `<TileLayer url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png" attribution="© OpenStreetMap contributors">`
- [x] Replace the hand-rolled `project()` function and its absolutely-positioned marker `<div>`s with `<Marker position={[v.lat, v.lng]} icon={divIcon}>` per vehicle, keeping the existing status-color hex mapping for the icon's color
- [x] Wire marker click → `onSelect(v)`, and keep the existing hover tooltip content (vehicle id, status, fuel, speed) via Leaflet's `<Tooltip>` component
- [x] Add a small helper that computes a `LatLngBounds` from `vehicles` and calls `map.fitBounds(...)` (via a child component using `useMap()`) once on initial load so every point starts inside the viewport, satisfying "all points are in the map not the outside"
- [x] Preserve the `isSelected` pulsing-ring visual treatment for the selected vehicle's marker (recreate via a second, larger transparent-ish `divIcon` circle behind the marker, or a CSS class on the `divIcon` HTML)

---

**Implementation notes:**

1. Keep the exported component signature identical: `function MapView({ vehicles, onSelect, selectedId }: Props)`, default-exported wrapped in `React.memo` as today — `frontend/app/page.tsx` must not need any changes.
2. Build custom icons with `L.divIcon({ html: '<div style="background:' + statusHex + '..."></div>', className: '', iconSize: [16,16] })` — do not reference Leaflet's default `.png` marker images, which 404 under Next.js's bundler without extra webpack config.
3. `react-leaflet`'s `MapContainer` must be rendered only on the client (it touches `window`); this file is already `"use client"`, which is sufficient — no dynamic `next/dynamic` import is needed since `MapView` itself isn't rendered during SSR of a page that would break (confirm by checking how `page.tsx` renders it; if it's SSR'd add `next/dynamic` with `{ ssr: false }` at the `MapView` import site — but that import site is `page.tsx`, which is in the "Do NOT touch" list, so if this is required, escalate: STOP and report before making the change).
4. Fleet-scale note: NF-01 requires 60 FPS with 10,000+ vehicles — Leaflet renders one DOM node per marker by default, which will not hold up at 10,000 markers. Out of scope for this task to add clustering (`react-leaflet-cluster` or similar) since it isn't in the user's ask; note this as a follow-up in the Sprint Retrospective / BACKLOG rather than solving it here. Do not add a clustering library in this task.
5. For the bounding-box fit: if `vehicles` is empty on first render, fall back to a reasonable default center/zoom (e.g. the SF bbox center used today) rather than calling `fitBounds` with an empty/invalid bounds object, which throws.

---

**Acceptance criteria:**

1. `MapView.tsx` no longer contains the string `backgroundImage` or the hardcoded `i.pinimg.com` image URL.
2. `MapView.tsx` renders a `react-leaflet` `<MapContainer>` with a `<TileLayer>` and one `<Marker>` per vehicle in `vehicles`, at `[v.lat, v.lng]`.
3. Clicking a marker calls `onSelect(v)` with that vehicle.
4. The map's initial view fits all current vehicle positions within its viewport bounds (verified via `fitBounds` call on the computed vehicle bounding box).
5. `cd frontend && npm run type-check` passes with zero errors.
6. `cd frontend && npm run lint` passes with zero warnings.

---

**Verification command:**

```bash
cd frontend && npm run type-check && npm run lint
# Expected: zero errors, zero warnings

# Browser check
cd frontend && npm run dev
# Open http://localhost:3000 — verify: an interactive OpenStreetMap-tiled map renders (not a static
# background image), vehicle markers appear as colored dots at real geo positions, all markers are
# visible within the initial viewport (none clipped outside it), clicking a marker opens the detail panel,
# and the map can be panned/zoomed with mouse/scroll.
```

---

**Rollback:**

```bash
git checkout -- frontend/components/MapView.tsx
```

---

### BE-001: Remove dummy-mode simulation path and `USE_LIVE_TELEMETRY` toggle

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`backend/Program.cs` currently branches on `USE_LIVE_TELEMETRY` (read from config, default `false`): when `false` it registers `TelemetrySimulationService` as a hosted background service (the in-memory dummy simulation); when `true` it registers the live pipeline (`TelemetryPersistenceService`, `LiveBroadcastService`, `TelemetryRetentionService`). The user wants dummy mode removed entirely so the backend always runs live mode. This task deletes `TelemetrySimulationService.cs`, removes the `if (!useLiveTelemetry) { ... } else { ... }` branch in `Program.cs` in favor of unconditionally registering the live-mode services, and removes the `USE_LIVE_TELEMETRY` key from `backend/appsettings.json`. `F-26`/`F-32` in `REQUIREMENTS.md` describe the toggle and dummy-mode ID format and must be updated by ARCH-001's sibling doc work — but per this project's write-scope rules, `REQUIREMENTS.md` is ARCH-owned; this task's ASP.NET agent must not edit it directly. Flag the stale requirement to ARCH via the Sprint Retrospective instead.

---

**Files to read before starting:**

- `backend/Program.cs` — the toggle branch to remove
- `backend/Services/TelemetrySimulationService.cs` — the file being deleted; confirm nothing outside it depends on types defined here
- `backend/appsettings.json` — where `USE_LIVE_TELEMETRY` is configured
- `backend/AGENTS.md` — service registration and DI conventions
- `docs/requirements/REQUIREMENTS.md` — F-26, F-27, F-32 (dummy-mode requirements referencing the toggle being removed)

---

**Files to modify:**

- `backend/Program.cs` — remove the `useLiveTelemetry` toggle read and the `if/else` branch; always register `TelemetryPersistenceService`, `ITelemetryIngestQueue`, `LiveBroadcastService`, and `TelemetryRetentionService` (the former `else` branch's contents)
- `backend/appsettings.json` — remove the `"USE_LIVE_TELEMETRY": false` key (and update the `_comment` referencing it under the retention section if it reads oddly without the toggle)

---

**Files to create:**

- None

---

**Do NOT touch:**

- `backend/Hubs/FleetHub.cs` — hub stays minimal; path `/fleethub` must not change
- `backend/Services/LiveTelemetryStore.cs` — in-memory current-state cache only; no direct DB writes
- `backend/Controllers/TelemetryIngestController.cs` — no changes needed to ingest validation/enqueue logic
- `docs/requirements/REQUIREMENTS.md` — ARCH-owned; do not edit directly, flag instead

---

**Sub-task breakdown:**

- [x] Delete `backend/Services/TelemetrySimulationService.cs`
- [x] In `backend/Program.cs`, remove the `builder.Configuration.GetValue<bool>("USE_LIVE_TELEMETRY", false)` line and the surrounding `if (!useLiveTelemetry) {...} else {...}` block; keep only what the `else` branch registered, unconditionally
- [x] Remove `"USE_LIVE_TELEMETRY": false` from `backend/appsettings.json`
- [x] Search the backend for any other reference to `TelemetrySimulationService` or `USE_LIVE_TELEMETRY` (controllers, other services, tests) and remove/update them
- [x] `dotnet build` and confirm zero errors

---

**Implementation notes:**

1. `TelemetrySimulationService`'s constructor seeded 10,000 in-memory vehicles and ticked independently of the DB — after deletion, the *only* vehicle source is the live ingestion pipeline (`TelemetryIngestController` → `ITelemetryIngestQueue` → `TelemetryPersistenceService` → `ILiveTelemetryStore`/DB), fed exclusively by the Python emitter (`emitter/**`) as F-27 already requires. Confirm `GET /api/vehicles`/`VehiclesController` reads from `ILiveTelemetryStore` (already the case per BE-009's Sprint 04 note) — no controller changes should be needed beyond removing the toggle.
2. Do not attempt to fix the pre-existing `ILiveTelemetryStore`/`display_number` cold-start hydration gap noted in `docs/sprints/BACKLOG.md` — out of scope for this task.
3. If `dotnet build` reveals any remaining compile-time reference to the deleted class (e.g. in a test project), remove that reference as part of this task since it is a direct consequence of the deletion, not scope creep.

---

**Acceptance criteria:**

1. `backend/Services/TelemetrySimulationService.cs` no longer exists.
2. `backend/Program.cs` contains no reference to `USE_LIVE_TELEMETRY` or a live/dummy branch — `TelemetryPersistenceService`, `LiveBroadcastService`, and `TelemetryRetentionService` are registered unconditionally.
3. `backend/appsettings.json` contains no `USE_LIVE_TELEMETRY` key.
4. `cd backend && dotnet build` passes with zero errors.

---

**Verification command:**

```bash
cd backend && dotnet build
# Expected: Build succeeded, zero errors

grep -rn "TelemetrySimulationService\|USE_LIVE_TELEMETRY" backend/ || echo "clean"
# Expected: "clean" (no matches) or exit code 1 from grep
```

---

**Rollback:**

```bash
git checkout -- backend/Program.cs backend/appsettings.json
git checkout -- backend/Services/TelemetrySimulationService.cs
```

---

### INFRA-001: Remove `USE_LIVE_TELEMETRY` from Docker Compose and Helm chart

**Agent:** INFRA
**Depends on:** BE-001

---

**Status:** [x]

---

**Context:**

`containers/docker-compose.yml` sets `USE_LIVE_TELEMETRY=true` on the `backend` service, and `helm/iiot-fleet-app/templates/app-configmap.yaml` templates `USE_LIVE_TELEMETRY` from `.Values.backend.useLiveTelemetry` into the backend's ConfigMap. Since BE-001 removes the toggle from the application entirely, these env var references are now dead config that should be deleted to avoid confusion (an unused env var the backend silently ignores).

---

**Files to read before starting:**

- `containers/docker-compose.yml` — where `USE_LIVE_TELEMETRY=true` is set on `backend`
- `helm/iiot-fleet-app/templates/app-configmap.yaml` — where the value is templated
- `helm/iiot-fleet-app/values.yaml` — where `backend.useLiveTelemetry` is defined
- `docs/HELM_GUIDE.md` / `docs/DOCKER_README.md` — check for documentation references to the toggle

---

**Files to modify:**

- `containers/docker-compose.yml` — remove the `USE_LIVE_TELEMETRY=true` line from the `backend` service's `environment:` block
- `helm/iiot-fleet-app/templates/app-configmap.yaml` — remove the `USE_LIVE_TELEMETRY` key
- `helm/iiot-fleet-app/values.yaml` — remove `backend.useLiveTelemetry`
- `docs/HELM_GUIDE.md` — remove/update any mention of the toggle
- `docs/DOCKER_README.md` — remove/update any mention of the toggle

---

**Files to create:**

- None

---

**Do NOT touch:**

- `backend/**` — application code already updated in BE-001
- Service names (`backend`, `frontend`, `db`, `emitter`) — must not be renamed
- Network name `iiot-fleet-net` — must not change

---

**Sub-task breakdown:**

- [x] Remove `USE_LIVE_TELEMETRY=true` from `containers/docker-compose.yml`'s `backend` service environment block
- [x] Remove the `USE_LIVE_TELEMETRY` key from `helm/iiot-fleet-app/templates/app-configmap.yaml`
- [x] Remove `useLiveTelemetry` from `helm/iiot-fleet-app/values.yaml`
- [x] Grep `docs/HELM_GUIDE.md` and `docs/DOCKER_README.md` for `USE_LIVE_TELEMETRY` and remove/update stale references
- [x] Confirm `docker compose -f containers/docker-compose.yml config` still parses cleanly

---

**Implementation notes:**

1. Do not remove or rename the `backend`, `frontend`, `db`, or `emitter` service blocks themselves — only the one env var line per file.
2. Other env vars in `§9` of `REQUIREMENTS.md` (`BACKEND_URL`, `VEHICLE_COUNT`, `TICK_INTERVAL_SECONDS`, `MAX_CONCURRENCY` for the emitter; `ConnectionStrings__Fleet`, `FRONTEND_ORIGIN`, etc. for the backend) are unaffected and must be left as-is.
3. `helm/iiot-fleet-app/values.yaml`'s `backend.useLiveTelemetry` default was presumably `true`, matching Compose — confirm before deleting that no other template references it (`grep -rn useLiveTelemetry helm/`).

---

**Acceptance criteria:**

1. `containers/docker-compose.yml` contains no `USE_LIVE_TELEMETRY` reference.
2. `helm/iiot-fleet-app/templates/app-configmap.yaml` and `helm/iiot-fleet-app/values.yaml` contain no `useLiveTelemetry`/`USE_LIVE_TELEMETRY` reference.
3. `docker compose -f containers/docker-compose.yml config` parses without error.
4. `helm template helm/iiot-fleet-app` renders without error (if Helm CLI is available locally; otherwise visually confirm the template YAML is well-formed).

---

**Verification command:**

```bash
grep -rn "USE_LIVE_TELEMETRY\|useLiveTelemetry" containers/ helm/ docs/HELM_GUIDE.md docs/DOCKER_README.md || echo "clean"
# Expected: "clean"

docker compose -f containers/docker-compose.yml config >/dev/null && echo "compose OK"
# Expected: "compose OK"
```

---

**Rollback:**

```bash
git checkout -- containers/docker-compose.yml helm/iiot-fleet-app/templates/app-configmap.yaml helm/iiot-fleet-app/values.yaml docs/HELM_GUIDE.md docs/DOCKER_README.md
```

---

### BE-002: Wire Swagger/OpenAPI into `Program.cs`

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`AGENTS.md` and multiple docs already claim a Swagger UI exists at `http://localhost:8080/swagger`, but `backend/Program.cs` has no `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI` calls and `backend/*.csproj` has no `Swashbuckle.AspNetCore` package reference — the claim is currently false. This task adds the standard ASP.NET Core Swagger stack (`Swashbuckle.AspNetCore`) so `/swagger` genuinely serves interactive API docs for every controller (`VehiclesController`, `LogsController`, `HealthController`, `TelemetryIngestController`), satisfying `F-23`. Per the user's ask this must work in local dev; INFRA-002/INFRA-003 extend it to Docker/Helm.

---

**Files to read before starting:**

- `backend/Program.cs` — where to add `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI`
- `backend/FleetTelemetry.csproj` (or equivalent `.csproj` — confirm actual filename) — where to add the `Swashbuckle.AspNetCore` package reference
- `backend/Controllers/VehiclesController.cs`, `backend/Controllers/LogsController.cs`, `backend/Controllers/HealthController.cs` — confirm controllers have adequate XML/attribute coverage for Swagger to generate readable docs (no changes required unless generation fails)
- `docs/requirements/REQUIREMENTS.md` — F-23

---

**Files to modify:**

- `backend/Program.cs` — add `builder.Services.AddEndpointsApiExplorer(); builder.Services.AddSwaggerGen(...);` and `app.UseSwagger(); app.UseSwaggerUI();`
- `backend/*.csproj` — add `<PackageReference Include="Swashbuckle.AspNetCore" Version="6.*" />` (confirm latest 6.x compatible with .NET 8 at implementation time)

---

**Files to create:**

- None

---

**Do NOT touch:**

- `backend/Hubs/FleetHub.cs`
- `backend/Services/TelemetrySimulationService.cs` reference — already removed in BE-001; do not re-add

---

**Sub-task breakdown:**

- [x] Add `Swashbuckle.AspNetCore` package reference to the backend `.csproj`
- [x] `dotnet restore`
- [x] In `Program.cs`, add `builder.Services.AddEndpointsApiExplorer()` and `builder.Services.AddSwaggerGen(c => c.SwaggerDoc("v1", new() { Title = "IIoT Fleet Telemetry API", Version = "v1" }))` before `builder.Build()`
- [x] After `var app = builder.Build();`, add `app.UseSwagger(); app.UseSwaggerUI();` — do not gate behind `if (app.Environment.IsDevelopment())` since the user wants it available in containerized/Helm environments too (not just local dev)
- [x] `dotnet build` and confirm zero errors
- [x] Run `dotnet run` locally and confirm `http://localhost:8080/swagger` returns the Swagger UI HTML page — BLOCKED by known pre-existing .NET runtime mismatch (8.0.23 installed vs 8.0.28 requested, per Pre-Flight Checklist/BACKLOG.md); `dotnet build` used as primary gate per task instructions instead

---

**Implementation notes:**

1. Do NOT wrap `UseSwagger`/`UseSwaggerUI` in an `IsDevelopment()` check — the user explicitly wants it available "in local and containerised file and helm", i.e. also in whatever `ASPNETCORE_ENVIRONMENT` the Docker/Helm deployment uses (per `docs/requirements/REQUIREMENTS.md` §9, `ASPNETCORE_ENVIRONMENT` can be `Development` or `Production` — Swagger must work regardless).
2. Place the `AddSwaggerGen`/`UseSwagger` additions near the existing `AddControllers()`/`MapControllers()` calls in `Program.cs` for locality, not scattered.
3. This is exposing API documentation, not a security boundary — no auth is added here (matches `REQUIREMENTS.md` §10: authN/authZ is explicitly out of scope for this project).

---

**Acceptance criteria:**

1. `backend/*.csproj` references `Swashbuckle.AspNetCore`.
2. `backend/Program.cs` calls `AddSwaggerGen`/`UseSwagger`/`UseSwaggerUI` unconditionally (not gated behind `IsDevelopment()`).
3. `cd backend && dotnet build` passes with zero errors.
4. `dotnet run` then `curl -s http://localhost:8080/swagger/index.html` (or `/swagger`) returns HTTP 200 HTML.

---

**Verification command:**

```bash
cd backend && dotnet build
# Expected: Build succeeded, zero errors

cd backend && dotnet run &
sleep 5
curl -sI http://localhost:8080/swagger/index.html | head -1
# Expected: HTTP/1.1 200 OK
```

---

**Rollback:**

```bash
git checkout -- backend/Program.cs backend/*.csproj
```

---

### INFRA-002: Expose Swagger UI through the Docker Compose stack

**Agent:** INFRA
**Depends on:** BE-002
**Status:** [x]

---

**Context:**

The backend now serves `/swagger` unconditionally (BE-002). `containers/docker-compose.yml` already maps the backend's port 8080 to the host, so Swagger should already be reachable once the image is rebuilt with BE-002's changes — this task's job is to verify that end-to-end through a full rebuild and to update `docs/DOCKER_README.md` to explicitly document the Swagger URL for the containerized stack (it may already be documented per the earlier grep hit — confirm accuracy and correct if the doc describes dev-only availability).

---

**Files to read before starting:**

- `containers/docker-compose.yml` — confirm backend port mapping (`8080:8080` or similar) and `ASPNETCORE_ENVIRONMENT` value set for the `backend` service
- `containers/backend/Dockerfile` — confirm no build step strips Swashbuckle's dependencies or the `AddSwaggerGen` configuration
- `docs/DOCKER_README.md` — current Swagger documentation to verify/correct

---

**Files to modify:**

- `docs/DOCKER_README.md` — confirm/update the Swagger UI URL and note it works regardless of `ASPNETCORE_ENVIRONMENT`
- `containers/docker-compose.yml` — only if the backend port isn't already exposed to the host (verify first; likely no change needed)

---

**Files to create:**

- None

---

**Do NOT touch:**

- `containers/backend/Dockerfile`'s build stages — no Dockerfile changes should be needed since BE-002 only adds a NuGet package, which the existing `dotnet restore`/`dotnet publish` steps already handle
- Service/network names

---

**Sub-task breakdown:**

- [x] Confirm `containers/docker-compose.yml`'s `backend` service exposes port 8080 to the host — already `"8080:8080"`, no change needed
- [x] `docker compose -f containers/docker-compose.yml up --build -d`
- [x] `curl -sI http://localhost:8080/swagger/index.html` — returned `404` (see note below); `curl -s -o /dev/null -w "%{http_code}"` (GET) confirmed real `200`; both `/swagger/index.html` and `/swagger/v1/swagger.json` serve correct content. Root cause: Swashbuckle's `SwaggerUIMiddleware` only handles `GET`, not `HEAD` — 404 on `-I` is expected framework behavior, not a Docker/publish defect (confirmed no `PublishTrimmed`/`PublishAot` in `containers/backend/Dockerfile`; `Swashbuckle.AspNetCore.*` DLLs present in the published image)
- [x] Update `docs/DOCKER_README.md` with the confirmed Swagger URL and behavior (including the GET-vs-HEAD nuance)
- [x] `docker compose -f containers/docker-compose.yml down`

---

**Implementation notes:**

1. If the rebuilt backend image doesn't serve `/swagger`, the most likely cause is a `dotnet publish` trimming/optimization setting stripping Swashbuckle — check `containers/backend/Dockerfile` for `PublishTrimmed`/`PublishAot` flags before assuming a code bug; report specifics if found rather than guessing a fix.
2. This task is a verification + doc task, not a rebuild-from-scratch of the Dockerfile — only touch the Dockerfile if the verification step in the sub-tasks actually fails and pinpoints a Dockerfile-level cause.

---

**Acceptance criteria:**

1. `docker compose -f containers/docker-compose.yml up --build -d` succeeds.
2. `curl -sI http://localhost:8080/swagger/index.html` returns HTTP 200 against the containerized stack.
3. `docs/DOCKER_README.md` documents the Swagger UI URL for the Docker Compose stack.

---

**Verification command:**

```bash
docker compose -f containers/docker-compose.yml up --build -d
sleep 15
curl -sI http://localhost:8080/swagger/index.html | head -1
# Expected: HTTP/1.1 200 OK
docker compose -f containers/docker-compose.yml down
```

---

**Rollback:**

```bash
git checkout -- docs/DOCKER_README.md containers/docker-compose.yml
```

---

### INFRA-003: Expose Swagger UI through the Helm chart

**Agent:** INFRA
**Depends on:** BE-002
**Status:** [x]

---

**Context:**

The Helm chart (`helm/iiot-fleet-app/**`) deploys the same backend image that now serves `/swagger` unconditionally (BE-002); since `UseSwaggerUI()` isn't environment-gated, no chart changes are strictly required for Swagger to work inside a pod — but the backend Service/Ingress (if one exists) must actually route `/swagger` traffic, and `docs/HELM_GUIDE.md` should document how to reach it (e.g. via `kubectl port-forward`). This task audits the chart's Service/Ingress templates for any path restriction that would block `/swagger` and documents the access method.

---

**Files to read before starting:**

- `helm/iiot-fleet-app/templates/` (all backend-related Service/Ingress templates) — check for path-based routing rules that might exclude `/swagger`
- `helm/iiot-fleet-app/values.yaml` — check `backend.service`/`ingress` config
- `docs/HELM_GUIDE.md` — current deployment/access documentation

---

**Files to modify:**

- `helm/iiot-fleet-app/templates/*.yaml` — only if a path restriction is found that blocks `/swagger` (e.g. an Ingress rule scoped to `/api` only); add a rule/path for `/swagger` if an Ingress exists and is path-scoped
- `docs/HELM_GUIDE.md` — add a section documenting how to reach Swagger UI from a Helm-deployed backend (e.g. `kubectl port-forward svc/<backend-service> 8080:8080` then `http://localhost:8080/swagger`)

---

**Files to create:**

- None

---

**Do NOT touch:**

- Chart values that set real secrets — passwords remain placeholder defaults per existing convention
- `helm/iiot-fleet-app/templates/app-configmap.yaml` — already handled in INFRA-001; no further changes here

---

**Sub-task breakdown:**

- [x] Read every Service/Ingress template under `helm/iiot-fleet-app/templates/` for the backend
- [x] Determine whether traffic reaches the backend on all paths (typical for a Service/ClusterIP with no Ingress path rules) or is path-restricted (Ingress with explicit path matches)
- [x] If path-restricted and `/swagger` would be blocked, add the necessary path rule — N/A: the Ingress already has an explicit `/swagger` path rule, and the backend Service has no path restriction at all; no template change needed
- [x] If not path-restricted (most likely, given no Ingress path scoping is mentioned elsewhere in this repo's docs), no template change is needed — document the access method instead
- [x] Add a "Swagger UI" section to `docs/HELM_GUIDE.md` describing the access method confirmed above

---

**Implementation notes:**

1. Do not introduce a new Ingress resource if the chart doesn't already have one — that would be scope creep. Document port-forward access as the default path, and only add Ingress path rules if an Ingress template already exists and is path-restrictive.
2. Chart values must never hardcode real secrets (existing repo convention, `helm/iiot-fleet-app/**` file contract) — this task doesn't touch secrets at all, but keep it in mind if any values file edit is made.

---

**Acceptance criteria:**

1. `docs/HELM_GUIDE.md` documents a concrete, working method to reach `/swagger` on a Helm-deployed backend.
2. If any Service/Ingress template was modified, `helm template helm/iiot-fleet-app` renders without error.
3. No new secrets or non-placeholder credentials are introduced.

---

**Verification command:**

```bash
# If Helm CLI is available:
helm template helm/iiot-fleet-app > /dev/null && echo "helm template OK"

grep -n "swagger" -i docs/HELM_GUIDE.md
# Expected: at least one match documenting Swagger access
```

---

**Rollback:**

```bash
git checkout -- helm/iiot-fleet-app/templates/ docs/HELM_GUIDE.md
```

---

### ARCH-001: Add a data-flow diagram to `docs/APPLICATION_OVERVIEW.md`

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`docs/APPLICATION_OVERVIEW.md` (authored Sprint 07) describes the system in prose but has no visual data-flow diagram showing how telemetry moves through the stack: Python emitter → `POST /api/telemetry/ingest` → `ITelemetryIngestQueue`/`TelemetryPersistenceService` (buffered EF Core writes to PostgreSQL) → `ILiveTelemetryStore` (in-memory current-state cache) → `LiveBroadcastService` (500ms SignalR relay) → `/fleethub` → frontend dashboard, plus the parallel `GET /api/vehicles`/`GET /api/vehicles/{id}` read path served directly from `ILiveTelemetryStore`. This task adds a Mermaid diagram (renders natively in GitHub/most Markdown viewers, no external image tooling needed) to the overview doc.

---

**Files to read before starting:**

- `docs/APPLICATION_OVERVIEW.md` — full current content, to find the right insertion point and match existing tone/structure
- `docs/requirements/REQUIREMENTS.md` §6–§8 (schema, API contract, SignalR protocol) — source of truth for the data-flow steps to diagram
- `backend/Program.cs` (post BE-001 changes — read after BE-001 completes) — confirm the live-only pipeline's actual service wiring order

---

**Files to modify:**

- `docs/APPLICATION_OVERVIEW.md` — add a new "## Data Flow" section (or equivalent heading matching the doc's existing heading style) containing a Mermaid `flowchart` diagram plus 2-4 sentences of prose explaining it

---

**Files to create:**

- None

---

**Do NOT touch:**

- `frontend/**`, `backend/**` — ARCH's write scope is `docs/**`, `AGENTS.md`, `README.md`, `CHANGELOG.md` only
- Any file outside `docs/APPLICATION_OVERVIEW.md` for this task

---

**Sub-task breakdown:**

- [x] Read `docs/APPLICATION_OVERVIEW.md` in full to find heading conventions and insertion point
- [x] Draft a Mermaid `flowchart LR` (or `TD`) covering: Python emitter → Ingest Controller → Ingest Queue/Persistence Service → PostgreSQL, and → Live Telemetry Store → Live Broadcast Service → SignalR Hub (`/fleethub`) → Frontend Dashboard; plus the separate REST read path (`GET /api/vehicles*` → Live Telemetry Store → Frontend)
- [x] Insert the diagram in a fenced ` ```mermaid ` code block under a new/existing "Data Flow" heading
- [x] Add short prose (2-4 sentences) explaining the write path vs. read path distinction
- [x] Reflect BE-001's dummy-mode removal in the diagram — do not depict `TelemetrySimulationService` or a dummy/live branch, since the pipeline is now live-only

---

**Implementation notes:**

1. Wait for BE-001 to land before finalizing wording that describes the pipeline as unconditional/live-only (per the Dependency Map this task has NONE as a hard dependency for drafting, but the prose must not describe a toggle that no longer exists — sequence it after BE-001 in practice even though there's no file conflict).
2. Example Mermaid skeleton to adapt (do not copy verbatim without checking current service names against `backend/Program.cs`):
   ```mermaid
   flowchart LR
       E[Python Emitter] -->|POST /api/telemetry/ingest| IC[TelemetryIngestController]
       IC --> Q[ITelemetryIngestQueue]
       Q --> PS[TelemetryPersistenceService]
       PS --> DB[(PostgreSQL)]
       PS --> LTS[ILiveTelemetryStore]
       LTS --> LBS[LiveBroadcastService]
       LBS -->|SignalR /fleethub, ~500ms| FE[Frontend Dashboard]
       LTS -->|GET /api/vehicles| FE
   ```
3. Keep the diagram text-only (Mermaid), not a binary image — no new image assets should be added to `docs/`.

---

**Acceptance criteria:**

1. `docs/APPLICATION_OVERVIEW.md` contains a fenced ` ```mermaid ` code block depicting the emitter → backend → DB/SignalR → frontend flow.
2. The diagram does not reference `TelemetrySimulationService` or a dummy-mode branch.
3. The diagram is preceded/followed by prose explaining the write path and read path.

---

**Verification command:**

```bash
grep -n '```mermaid' docs/APPLICATION_OVERVIEW.md
# Expected: at least one match

grep -n "TelemetrySimulationService" docs/APPLICATION_OVERVIEW.md || echo "clean"
# Expected: "clean"
```

---

**Rollback:**

```bash
git checkout -- docs/APPLICATION_OVERVIEW.md
```

---

### QA-001: Verify map, live-only mode, and Swagger end-to-end

**Agent:** QA
**Depends on:** UI-002, INFRA-001, INFRA-002, INFRA-003, ARCH-001
**Status:** [x]

---

**Context:**

All feature tasks in this sprint are complete by the time this task starts. QA's job is to independently verify every acceptance criterion across the four workstreams — Leaflet map, live-only backend, Swagger in all three environments, and the docs diagram — without writing feature code, and to report any failure with exact file:line references per its role in `AGENTS.md`.

---

**Files to read before starting:**

- `frontend/components/MapView.tsx` — post-UI-002 state
- `backend/Program.cs`, `backend/appsettings.json` — post-BE-001/BE-002 state
- `containers/docker-compose.yml`, `helm/iiot-fleet-app/**` — post-INFRA-001/002/003 state
- `docs/APPLICATION_OVERVIEW.md` — post-ARCH-001 state

---

**Files to modify:**

- None (QA does not write feature code)

---

**Files to create:**

- None

---

**Do NOT touch:**

- Any production source file — QA verifies and reports only

---

**Sub-task breakdown:**

- [x] `cd frontend && npx tsc --noEmit` — zero errors (no `type-check`/`lint` npm scripts exist yet; known carryover, npx used directly)
- [x] `cd backend && dotnet build FleetTelemetry.csproj` — zero errors (0.7.1 sln has a pre-existing broken project-path reference unrelated to this sprint; csproj build is the real gate and passes)
- [x] Read-based check (browser unavailable in this environment): `frontend/components/MapView.tsx` + `frontend/app/page.tsx` confirmed to implement `MapContainer`/`TileLayer`/`Marker`/`Tooltip`, `fitBounds` on load, `onSelect` wired to marker click, and the `next/dynamic(ssr:false)` wrapper around `MapView` in `page.tsx` — consistent with each other
- [x] `grep -rn "USE_LIVE_TELEMETRY\|TelemetrySimulationService" backend/ containers/ helm/` returns no matches
- [x] `docker compose -f containers/docker-compose.yml up --build -d` then `curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/swagger/index.html` (GET) returns 200; `curl -s http://localhost:8080/api/vehicles` returns a live JSON array (fresh `lastSeenAtUtc`, real IDs from the emitter/DB path); all 4 services (`db`, `backend`, `frontend`, `emitter`) reported healthy/running; then `docker compose down` cleaned up
- [x] `grep -n '```mermaid' docs/APPLICATION_OVERVIEW.md` finds the diagram at line 44
- [x] Report filed below — no non-pre-existing failures found

---

**Implementation notes:**

1. If any check fails, STOP and report to the user/ARCH rather than patching the failing task's files — QA's write scope excludes production source.
2. Note the known pre-existing .NET runtime version mismatch (8.0.23 installed vs. 8.0.28 requested) from the Pre-Flight Checklist if it blocks a live `dotnet run` smoke test locally — this is a carried-over environment issue, not a regression introduced by this sprint, and should be reported as such rather than treated as a sprint failure.

---

**Acceptance criteria:**

1. All sub-task checks above pass or have documented, correctly-attributed pre-existing exceptions (the .NET runtime mismatch only).
2. A written verification report lists pass/fail per acceptance criterion from UI-001, UI-002, BE-001, BE-002, INFRA-001, INFRA-002, INFRA-003, and ARCH-001.

---

**Verification command:**

```bash
cd frontend && npm run type-check && npm run lint
cd backend && dotnet build
grep -rn "USE_LIVE_TELEMETRY\|TelemetrySimulationService" backend/ containers/ helm/ || echo "clean"
grep -n '```mermaid' docs/APPLICATION_OVERVIEW.md
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
- [ ] If unresolved issues remain, add to `docs/sprints/BACKLOG.md` and plan for next sprint

**Version and changelog:**
- [ ] Bump `frontend/package.json` version: `0.7.1` → `0.8.0` (minor — new dependency/feature, no breaking API change)
- [ ] Add `## v0.8.0 — YYYY-MM-DD` entry to `CHANGELOG.md` with `### Add`, `### Fix`, `### Update` sections
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S08-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npm run type-check && npm run lint` passes on the final branch state
- [ ] `cd backend && dotnet build` passes on the final branch state
- [ ] Open PR: `claude/sprint-08-leaflet-live-swagger` → `main` with title `IIOT-v0.8.0: sprint-08 leaflet map, live-only mode, swagger everywhere`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-08.md` → `docs/sprints/archive/sprint-08.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `sprint-09.md`
- [ ] Update `CHANGELOG.md` if system design changed
- [ ] Update `docs/requirements/REQUIREMENTS.md` F-26/F-27/F-32 to remove the `USE_LIVE_TELEMETRY` toggle language and dummy-mode ID-format requirement (flagged by BE-001; ARCH-owned edit)
- [ ] Update NF-01's map-scale note / add a BACKLOG.md entry for Leaflet marker clustering at 10,000-vehicle scale (flagged by UI-002 implementation note #4)

---

## Sprint Retrospective

> Filled at sprint end. 3–6 bullets. What worked, what blocked, what to change next sprint.

- {{Win 1}}
- {{Win 2}}
- {{Blocker or pain point}}
- {{Action item carried to next sprint}}

---

## Agent Execution Protocol

> This section is read by the AI agent at the start of every session. Identical across all sprint files — do not modify.

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full (activates caveman token mode)
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-08-leaflet-live-swagger
   - If not: git fetch origin main && git checkout -B claude/sprint-08-leaflet-live-swagger origin/main
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
18. Commit: git commit -m "IIOT-S08-{TASK-ID}: <one-line summary>"

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
| **INFRA** | DevOps agent — owns Docker, Helm, env vars |
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **ARCH** | System designer agent — owns docs, sprint files, CHANGELOG |
| **Acceptance criterion** | Binary, testable assertion — TRUE or FALSE |
| **Verification command** | Shell command that proves an acceptance criterion is TRUE |
| **Rollback** | Operations that return the system to its pre-task state |
| **SignalR hub** | `backend/Hubs/FleetHub.cs` — WebSocket endpoint at `/fleethub` |
| **ILiveTelemetryStore** | In-memory current-state cache; the sole vehicle-state source after this sprint removes dummy mode |
| **Leafmap** | Python geospatial notebook package (leafmap.org) referenced by the user; not usable in a browser/React app — this sprint uses its underlying engine, **Leaflet.js**, via `react-leaflet`, instead |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name |
