# IIoT Fleet Telemetry System — AI Orchestration Brain

---

## System

**Project:** IIoT Fleet Telemetry System — real-time monitoring of 10,000+ industrial assets.

---

**Frontend Local URL:** `http://localhost:3000`
**Backend Local Swagger:** `http://localhost:8080/swagger`
**SignalR Hub:** `http://localhost:8080/fleethub`

---

**Stack summary:**

| Layer | Technology |
|-------|-----------|
| Frontend | Next.js 15 (App Router) + TypeScript + Tailwind CSS + Zustand + SignalR |
| Backend | ASP.NET Core 8 Web API + SignalR + MessagePack |
| Database | PostgreSQL (EF Core integration — Sprint 01) |
| DevOps | Docker + Docker Compose + GitHub Actions |
| AI Tools | Claude Code, GitHub Copilot, Gemini |

---

**Full technical spec:** `docs/requirements/REQUIREMENTS.md`
**Active sprint:** See `## Current Sprint` at the bottom of this file.

---

### ARCH — Architect

| Attribute | Value |
|-----------|-------|
| **Read scope** | All files in the repository |
| **Write scope** | `docs/**`, `AGENTS.md`, `README.md`, `CHANGELOG.md` |
| **Prohibited writes** | Any file under `frontend/`, `backend/`, `.github/workflows/` |
| **Responsibilities** | Maintain requirements, sprint files, CHANGELOG, and this file. Design system-level plans. Never write application code. |

---

### NEXT — Next.js Engineer (Frontend)

| Attribute | Value |
|-----------|-------|
| **Read scope** | `frontend/**`, `AGENTS.md`, `frontend/AGENTS.md`, active sprint file |
| **Write scope** | `frontend/**` only |
| **Prohibited writes** | `backend/**`, `.github/workflows/**`, `docs/**`, root config files |
| **Responsibilities** | Build and maintain the Next.js 15 App Router dashboard. Own components, Zustand stores, SignalR client hook, API route handlers under `app/api/`, and all TypeScript types. |
| **Must read before touching code** | `frontend/AGENTS.md` in full |

**Key conventions:**
- All components in `frontend/components/` are client components unless explicitly named `*.server.tsx`
- State: Zustand stores in `frontend/store/` — never use React Context for cross-component state
- Real-time: SignalR connection managed in `frontend/app/page.tsx` — one connection per session
- Virtualization: always use `@tanstack/react-virtual` for lists > 100 items
- Styling: Tailwind utility classes only; no inline styles; theme tokens in `tailwind.config.js`
- Type safety: `npm run type-check` must pass with zero errors before every commit

---

### ASP.NET — .NET Core Engineer (Web API + Swagger)

| Attribute | Value |
|-----------|-------|
| **Read scope** | `backend/**`, `AGENTS.md`, `backend/AGENTS.md`, active sprint file |
| **Write scope** | `backend/**` only |
| **Prohibited writes** | `frontend/**`, `.github/workflows/**`, `docs/**` |
| **Responsibilities** | Build and maintain ASP.NET Core 8 Web API. Own controllers, SignalR hub, MessagePack models, EF Core DbContext, PostgreSQL migrations, and background simulation service. |
| **Must read before touching code** | `backend/AGENTS.md` in full |

**Key conventions:**
- Controllers in `backend/Controllers/` — thin, delegate to services
- SignalR hub at `/fleethub` — never change this path without updating frontend env vars
- MessagePack models in `backend/Models/` — all properties must have `[Key(N)]` attribute
- EF Core: migrations via `dotnet ef migrations add <Name>` — never hand-edit migration files
- CORS: configured via `FRONTEND_ORIGIN` env var (see `Program.cs`)

---

### INFRA — Infrastructure Engineer

| Attribute | Value |
|-----------|-------|
| **Read scope** | All files in the repository |
| **Write scope** | `containers/**` (Dockerfiles + `docker-compose.yml`), `.env*` files, `helm/**` |
| **Prohibited writes** | Application source files under `frontend/src/` or `backend/` (non-config); `emitter/**` (owned by EMIT — INFRA still owns `containers/emitter/Dockerfile` and the emitter's env wiring in Compose/Helm, just not `emitter/*.py`) |
| **Responsibilities** | Maintain the Docker Compose stack (`containers/docker-compose.yml`), the per-service Dockerfiles under `containers/`, environment variable management, health checks, and container networking. No GitHub Actions CI/CD — this project doesn't run one. |

**Key conventions:**
- Backend service name in Compose: `backend` (frontend references it as `http://backend:8080`)
- Network name: `iiot-fleet-net`
- Health checks: backend at `/`, frontend at `/` — 30s interval, 3 retries
- Environment: `FRONTEND_ORIGIN`, `ADDITIONAL_FRONTEND_ORIGINS` (backend CORS), `NEXT_PUBLIC_API_URL` (frontend)

---

### EMIT — IIoT Emitter Engineer

| Attribute | Value |
|-----------|-------|
| **Read scope** | `emitter/**`, `AGENTS.md`, `docs/requirements/REQUIREMENTS.md`, active sprint file |
| **Write scope** | `emitter/**` only (`emitter.py`, `requirements.txt`, any new modules under `emitter/`) |
| **Prohibited writes** | `backend/**`, `frontend/**`, `containers/**`, `helm/**`, `docs/**` |
| **Responsibilities** | Simulate a realistic IIoT vehicle fleet — land-constrained geo-positions, plausible waypoint-to-waypoint motion, and correlated telemetry evolution. Never emits for vehicle IDs not sourced from `GET /api/vehicles/metadata`. |
| **Must read before touching code** | `.claude/skills/iiot-emitter/SKILL.md` in full |

**Key conventions:**
- Position sampling MUST be land-constrained (curated waypoint list or road-graph snap) — never raw `random.uniform` across the full lat/lng bounding box, since the box includes water
- Payload keys are camelCase, matching `TelemetryIngestRequest` exactly
- Single shared `aiohttp.ClientSession` + `TCPConnector`/`Semaphore(MAX_CONCURRENCY)` — never one connection per vehicle
- One failed vehicle tick must never crash the process

---

### QA — Quality Assurance

| Attribute | Value |
|-----------|-------|
| **Read scope** | All files in the repository |
| **Write scope** | `frontend/**/*.test.*`, `backend/**/*Tests*`, `docs/sprints/**` (acceptance criteria updates only) |
| **Prohibited writes** | Production source files |
| **Responsibilities** | Verify all acceptance criteria pass. Run type-check, ESLint, and test suites. Report failures with exact error output and file:line references. |

**Verification commands:**
```bash
# Frontend
cd frontend && npm run type-check   # must pass with zero errors
cd frontend && npm run lint          # must pass with zero warnings

# Backend
cd backend && dotnet build           # must succeed with zero errors
cd backend && dotnet test            # must pass all tests (when test project exists)

# Docker stack
docker compose -f containers/docker-compose.yml up --build -d
curl http://localhost:8080/api/vehicles   # must return JSON array
curl http://localhost:3000               # must return HTTP 200
```

---

## Execution Rules

These rules are mandatory. Every agent MUST apply them on every task, every time.

1. **ALWAYS** read the active sprint file before beginning any task.
2. **ALWAYS** read the relevant `AGENTS.md` file for the subsystem being modified (`frontend/AGENTS.md`, `backend/AGENTS.md`) before writing code.
3. **ALWAYS** update the sprint file task checkbox from `[ ]` to `[x]` immediately after completing a task.
4. **NEVER** commit if `npm run type-check` or `npm run lint` report errors (frontend tasks).
5. **NEVER** commit if `dotnet build` reports errors (backend tasks).
6. **ALWAYS** commit with format: `IIOT-S{{NN}}-{{TASK-ID}}: <one-line summary>`.

---

## File Contracts

These conventions are immutable. Agents MUST NOT break them.

| File/Path | Owner | Rule |
|-----------|-------|------|
| `frontend/app/page.tsx` | NEXT | Single SignalR connection; vehicle state in `useRef<Map>` for O(1) updates |
| `frontend/store/*.ts` | NEXT | Zustand stores only; no Redux, no Context API |
| `frontend/components/*.tsx` | NEXT | Client components by default; no server-side data fetching in components |
| `backend/Hubs/FleetHub.cs` | ASP.NET | Hub path MUST remain `/fleethub`; hub itself stays minimal |
| `backend/Services/TelemetrySimulationService.cs` | ASP.NET | Background service; do not add HTTP dependencies |
| `backend/Models/*.cs` | ASP.NET | MessagePack models need `[MessagePackObject]` + `[Key(N)]`; API DTOs use `[JsonPropertyName]` |
| `containers/docker-compose.yml` | INFRA | Service names `backend`, `frontend`, `db`, `emitter` must not be renamed; Dockerfiles live under `containers/<service>/Dockerfile` with `build.context` pointed back at the real source dir (`../backend`, `../frontend`, `../emitter`) |
| `docs/sprints/sprint-*.md` | ARCH | Created from `docs/sprints/archive/TEMPLATE.md`; never edited mid-sprint by non-ARCH agents |
| `CHANGELOG.md` | ARCH | Updated only at sprint end; format: `## vX.Y.Z — YYYY-MM-DD` |
| `backend/Services/LiveTelemetryStore.cs` | ASP.NET | In-memory current-state cache only; no direct DB writes — persistence is `TelemetryPersistenceService`'s job |
| `backend/Controllers/TelemetryIngestController.cs` | ASP.NET | Validates payload; never calls `SaveChangesAsync` synchronously — only enqueues to the buffered writer |
| `emitter/**` | INFRA | Outbound HTTP client only; must only use vehicle IDs sourced from `GET /api/vehicles/metadata` |
| `backend/Services/HubConnectionTracker.cs` | ASP.NET | Tracks active `/fleethub` SignalR connection count/state in memory; read by `/api/health/signalr`; no DB access |
| `backend/Services/TelemetryRetentionService.cs` | ASP.NET | Background service that deletes `telemetry_snapshots` rows older than `TelemetryRetention__RetentionDays`; batches deletes by `TelemetryRetention__DeleteBatchSize`; does not create new tables |
| `frontend/components/ConnectionStatus.tsx` | NEXT | Client component; renders SignalR connection-status indicator in the dashboard header; polls/consumes `/api/health/signalr` or the client SignalR connection state, not both as sources of truth |
| `PATCH /api/vehicles/{id}` | ASP.NET | Edits `driver_name`/`display_number` only; MUST NEVER rename the `id` primary key (FK target for `telemetry_snapshots`/`vehicle_logs` and the exact string the Python emitter sources from `GET /api/vehicles/metadata`) |
| `helm/iiot-fleet-app/**` | INFRA | Chart values must never hardcode real secrets; passwords are placeholder defaults in `values.yaml`, overridden at install time |

---

## Sprint Loop

This is the mandatory execution protocol for every sprint task.

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full
5. Confirm branch matches sprint metadata
6. Identify the first task where Status: [ ] and all dependencies are [x]
7. Read every file listed under "Files to read before starting" for that task

TASK EXECUTION
──────────────
8.  Walk the "Sub-task breakdown" list top-to-bottom — tick each sub-step as completed
9.  Implement the task following "Implementation notes" exactly
10. Do NOT modify any file listed under "Do NOT touch"
11. After implementation, run the "Verification command" exactly as written
12. If verification fails: fix and re-run — do not mark complete until passing
13. If verification passes: update Status from [ ] to [x] in this sprint file
14. Tick the matching entry in "## Task Index"
15. Commit: git commit -m "IIOT-S{{NN}}-{{TASK-ID}}: <one-line summary>"

BETWEEN TASKS
─────────────
16. Return to step 6 — pick the next unchecked task
17. If all tasks are [x]: run the Sprint-End Checklist

BLOCKERS
────────
18. If a "Files to read" file does not exist: STOP, report to user
19. If a verification command fails with an unresolvable error: STOP, report to user
20. If any acceptance criterion cannot be made TRUE without scope creep: STOP, report to user
```

---

## Local Dev Setup

### Prerequisites

- Node.js 20+
- .NET 8 SDK
- PostgreSQL 16 (running locally on port 5432)
- Docker Desktop (for containerized runs)

### Frontend (Next.js 15)

```bash
cd frontend
npm install
cp .env.example .env.local   # set NEXT_PUBLIC_API_URL=http://localhost:8080
npm run dev                   # http://localhost:3000
```

### Backend (ASP.NET Core 8 + PostgreSQL)

```bash
cd backend
# Set connection string
export ConnectionStrings__Fleet="Host=localhost;Database=fleet_telemetry;Username=postgres;Password=yourpassword"
dotnet restore
dotnet ef database update     # apply migrations (after Sprint 01)
dotnet run                    # http://localhost:8080
```

### Full Stack (Docker)

```bash
docker compose -f containers/docker-compose.yml up --build
# Frontend: http://localhost:3000
# Backend API: http://localhost:8080
# Swagger UI: http://localhost:8080/swagger
```

---

## Claude Code Commands (`.claude/commands/`)

| Command | File | Purpose |
|---------|------|---------|
| `/devops` | `.claude/commands/devops.md` | Run Docker Compose, check health, tail logs |
| `/review-ux` | `.claude/commands/review-ux.md` | Open Chrome, verify dashboard renders correctly |
| `/analyze-analytics` | `.claude/commands/analyze-analytics.md` | Check SignalR stats, latency, alert counts |
| `/chrome` | `.claude/commands/chrome.md` | Launch Chrome at a URL, capture console output |

## Skills (`.claude/skills/`)

| Skill | File | Activates |
|-------|------|-----------|
| `sprint` | `.claude/skills/sprint/SKILL.md` | Sprint authoring protocol + caveman token rules |
| `nextjs` | `.claude/skills/nextjs/SKILL.md` | Next.js 15 App Router patterns and conventions |
| `asp-dot-net-core` | `.claude/skills/asp-dot-net-core/SKILL.md` | ASP.NET Core 8 patterns, SignalR, EF Core |
| `postgre-sql` | `.claude/skills/postgre-sql/SKILL.md` | PostgreSQL schema conventions, migrations |
| `devops` | `.claude/skills/devops/SKILL.md` | Docker, GitHub Actions, env var management |
| `caveman` | `.claude/skills/caveman/SKILL.md` | Token-compression communication mode |
| `iiot-emitter` | `.claude/skills/iiot-emitter/SKILL.md` | Land-constrained vehicle position simulation, route/telemetry realism |

## Agents (`.claude/agents/`)

| Agent File | Role Code | Description |
|-----------|-----------|-------------|
| `frontend-engineer.md` | NEXT | Next.js 15 frontend specialist |
| `backend-engineer.md` | ASP.NET | ASP.NET Core 8 + PostgreSQL specialist |
| `system-designer.md` | ARCH | Architecture + requirements + sprint authoring |
| `team-lead.md` | LEAD | PR review, agent coordination, conventions enforcement |
| `devops-architech.md` | INFRA | Docker, CI/CD, infrastructure |
| `quality-analyst.md` | QA | Testing, type-check, ESLint, acceptance criteria |
| `iiot-emitter.md` | EMIT | Python emitter — realistic vehicle telemetry + geo-position simulation |
| `analyst.md` | ANALYST | Performance analysis, telemetry metrics |
| `debugger.md` | DEBUG | Root-cause analysis, error tracing |

---

## Key Knowledge Base Documents

| Document | Path |
|----------|------|
| Project Requirements | `docs/requirements/REQUIREMENTS.md` |
| Application Overview | `docs/APPLICATION_OVERVIEW.md` (authored in Sprint 07) |
| DevOps Learning Guides | `docs/devops-learn/` — `Docker_Compose.md`, `Helm.md`, `K8s.md` (authored in Sprint 07) |
| Sprint Template | `docs/sprints/archive/TEMPLATE.md` |
| Docker Instructions | `docs/DOCKER_README.md` |
| Helm/Kubernetes Deployment Guide | `docs/HELM_GUIDE.md` |
| SDD Workflow | `docs/SDD_WORKFLOW.md` |
| Project Overview | `README.md` |
| Changelog | `CHANGELOG.md` |

---

## Current Sprint

**Active:** None active.

**Previous:** Sprint 09 — `docs/sprints/archive/sprint-09.md`. Fixed two bugs surfaced by Sprint 08's Leaflet map: the emitter's naive bounding-box `random.uniform` position sampling put vehicles in San Francisco Bay/ocean instead of on land (fixed with a curated 35-point on-land waypoint list + waypoint-to-waypoint motion, `emitter/emitter.py`), and unclustered per-vehicle Leaflet markers lagged the dashboard at fleet scale (fixed with `@changey/react-leaflet-markercluster`, `frontend/components/MapView.tsx`). Introduced the new EMIT agent/skill (`.claude/agents/iiot-emitter.md`, `.claude/skills/iiot-emitter/SKILL.md`) to own realistic, land-constrained fleet simulation going forward. All 5 tasks (DEBUG-001, EMIT-001, UI-003, QA-002, LEAD-001) `[x]`; team-lead GO verdict, one non-blocking follow-up flagged (stale dead-code filter/comment in `MapView.tsx` lines 74/77, carried to `docs/sprints/BACKLOG.md`). `v0.8.1`, merged to `main`.

**Previous:** Sprint 08 — `docs/sprints/archive/sprint-08.md`. Replaced the static-background-image map with a real interactive Leaflet map (`react-leaflet`); removed dummy mode (`TelemetrySimulationService`, `USE_LIVE_TELEMETRY`) everywhere so the backend always runs live emitter-fed mode; wired real Swagger UI into local/Docker/Helm; added a Mermaid data-flow diagram to `docs/APPLICATION_OVERVIEW.md`. All 9 tasks + QA `[x]`; team-lead review passed with no compliance blockers. `v0.8.0`, merged to `main`.

**Still open (carried over, not in Sprint 07's scope):** missing frontend `lint`/`type-check` npm scripts + ESLint config; full-scale `VEHICLE_COUNT=10000` NF-01/NF-03 validation; the `ILiveTelemetryStore`/`display_number` cold-start hydration gap found during Sprint 04's `BE-009`; this dev machine's installed .NET runtimes (top out at 8.0.23) don't match what the built backend binary requests (8.0.28), which blocked QA-007's live-mode runtime smoke test in Sprint 07 (build itself is unaffected); `backend/fleet-telemetry-system.sln` has a stale doubled relative path (`backend/backend/FleetTelemetry.csproj`) causing `dotnet build` on the bare `.sln` to fail with MSB3202 (building the `.csproj` directly works; found during Sprint 09's QA-002). The standalone CI-fix branch `claude/fix-docker-image-ci-workflow` is now moot — Sprint 07's infra addendum removed the GitHub Actions workflow entirely. See `docs/sprints/BACKLOG.md` for details.

**Previous:** Sprint 07 — `docs/sprints/archive/sprint-07.md`. Main scope (10 tasks, `v0.7.0`): removed the "Hide Inactive"/"Focused View" sidebar controls and the underlying client-side "inactive vehicle" concept entirely; replaced `VehicleStatusEvaluator`/`TelemetrySimulationService` status thresholds and the simulation rebalancer's fixed caps with new thresholds and ranged targets — offline 40-100, danger 100-400, warning 500-800, active = remainder; updated `REQUIREMENTS.md` §4.1/§4.2 and removed F-33/F-34/§4.4; authored `docs/APPLICATION_OVERVIEW.md` and three new `docs/devops-learn/` guides — Docker Compose, Helm, Kubernetes. Operator addendum on the same branch/PR (8 tasks, `v0.7.1`): Dockerfiles moved to `containers/<service>/Dockerfile`; `db` now pulls `postgres:16-alpine` directly (`db/Dockerfile` removed); Helm chart templates split into per-service folders; `DOCKER_README.md` moved to `docs/`; `iiot-emitter` renamed to `emitter` throughout; GitHub Actions workflow removed entirely. All 18 tasks `[x]`.

**Roadmap:** `docs/sprints/BACKLOG.md` — tracks the still-open carryover items: the standalone CI build fix on `claude/fix-docker-image-ci-workflow` (not yet merged), the `ILiveTelemetryStore`/`display_number` cold-start hydration gap found during Sprint 04 (BE-009's known follow-up), the missing frontend `lint`/`type-check` npm scripts + ESLint config (carried over from Sprint 03), and a full-scale (`VEHICLE_COUNT=10000`) NF-01/NF-03 validation follow-up.

> To start a new sprint: invoke the `sprint` skill (`.claude/skills/sprint/SKILL.md`). The skill copies `docs/sprints/archive/TEMPLATE.md`, fills every task block, registers the file here, and never branches from anything other than `origin/main`.
