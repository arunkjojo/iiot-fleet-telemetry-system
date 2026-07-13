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
| **Write scope** | `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile`, `.github/workflows/**`, `.env*` files, `iiot-emitter/**` |
| **Prohibited writes** | Application source files under `frontend/src/` or `backend/` (non-config) |
| **Responsibilities** | Maintain Docker Compose stack, GitHub Actions CI/CD pipeline, environment variable management, health checks, and container networking. |

**Key conventions:**
- Backend service name in Compose: `backend` (frontend references it as `http://backend:8080`)
- Network name: `iiot-fleet-net`
- Health checks: backend at `/`, frontend at `/` — 30s interval, 3 retries
- Environment: `FRONTEND_ORIGIN`, `ADDITIONAL_FRONTEND_ORIGINS` (backend CORS), `NEXT_PUBLIC_API_URL` (frontend)

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
docker-compose up --build -d
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
| `docker-compose.yml` | INFRA | Service names `backend` and `frontend` must not be renamed |
| `docs/sprints/sprint-*.md` | ARCH | Created from `docs/sprints/archive/TEMPLATE.md`; never edited mid-sprint by non-ARCH agents |
| `CHANGELOG.md` | ARCH | Updated only at sprint end; format: `## vX.Y.Z — YYYY-MM-DD` |
| `backend/Services/LiveTelemetryStore.cs` | ASP.NET | In-memory current-state cache only; no direct DB writes — persistence is `TelemetryPersistenceService`'s job |
| `backend/Controllers/TelemetryIngestController.cs` | ASP.NET | Validates payload; never calls `SaveChangesAsync` synchronously — only enqueues to the buffered writer |
| `iiot-emitter/**` | INFRA | Outbound HTTP client only; must only use vehicle IDs sourced from `GET /api/vehicles/metadata` |
| `backend/Services/HubConnectionTracker.cs` | ASP.NET | Tracks active `/fleethub` SignalR connection count/state in memory; read by `/api/health/signalr`; no DB access |
| `backend/Services/TelemetryRetentionService.cs` | ASP.NET | Background service that deletes `telemetry_snapshots` rows older than `TelemetryRetention__RetentionDays`; batches deletes by `TelemetryRetention__DeleteBatchSize`; does not create new tables |
| `frontend/components/ConnectionStatus.tsx` | NEXT | Client component; renders SignalR connection-status indicator in the dashboard header; polls/consumes `/api/health/signalr` or the client SignalR connection state, not both as sources of truth |

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
docker-compose up --build
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

## Agents (`.claude/agents/`)

| Agent File | Role Code | Description |
|-----------|-----------|-------------|
| `frontend-engineer.md` | NEXT | Next.js 15 frontend specialist |
| `backend-engineer.md` | ASP.NET | ASP.NET Core 8 + PostgreSQL specialist |
| `system-designer.md` | ARCH | Architecture + requirements + sprint authoring |
| `team-lead.md` | LEAD | PR review, agent coordination, conventions enforcement |
| `devops-architech.md` | INFRA | Docker, CI/CD, infrastructure |
| `quality-analyst.md` | QA | Testing, type-check, ESLint, acceptance criteria |
| `analyst.md` | ANALYST | Performance analysis, telemetry metrics |
| `debugger.md` | DEBUG | Root-cause analysis, error tracing |

---

## Key Knowledge Base Documents

| Document | Path |
|----------|------|
| Project Requirements | `docs/requirements/REQUIREMENTS.md` |
| Sprint Template | `docs/sprints/archive/TEMPLATE.md` |
| Docker Instructions | `DOCKER_README.md` |
| Project Overview | `README.md` |
| Changelog | `CHANGELOG.md` |

---

## Current Sprint

**Active:** Sprint 03 — `docs/sprints/sprint-03.md`
**Branch:** `claude/sprint-03-telemetry-reliability-hardening`
**Note:** Sprint 03 adds SignalR connection-status visibility (backend `HubConnectionTracker` + `/api/health/signalr`, frontend header indicator), a client-side "inactive vehicle" concept (sustained speed=0 for 60s+, does not change the server-side `status` enum), and a telemetry retention/cleanup background service closing ADR-001 action item #5. It is the first of three themed sprints split from a larger 9-task operator brief (2026-07-13) — see `docs/sprints/BACKLOG.md` for Sprint 04 (UX/search) and Sprint 05 (infra/docs) scope.

**Previous:** Sprint 02 — `docs/sprints/sprint-02.md` (live IIoT telemetry ingestion — Python emitter, PostgreSQL persistence, SignalR rebroadcast; all tasks `[x]` but not yet moved to `archive/` — Sprint 02's own ARCH-002 sprint-end wrap-up was not fully run)

**Roadmap:** `docs/sprints/BACKLOG.md` — Sprint 04 (editable vehicle/driver fields, search, UI polish, focused view) and Sprint 05 (CI fix, project documentation), scoped but not yet authored as full sprint files.

> To start a new sprint: invoke the `sprint` skill (`.claude/skills/sprint/SKILL.md`). The skill copies `docs/sprints/archive/TEMPLATE.md`, fills every task block, registers the file here, and never branches from anything other than `origin/main`.
