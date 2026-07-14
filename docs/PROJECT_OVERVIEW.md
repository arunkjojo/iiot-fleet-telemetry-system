# Project Overview

This is the map, not the territory: a short onboarding read for a new team member or evaluator.
Every section below summarizes and links to the authoritative source document rather than
duplicating it — when the two disagree, the linked source wins.

1. [What This Is](#1-what-this-is)
2. [Architecture](#2-architecture)
3. [Key Design Decisions](#3-key-design-decisions)
4. [DevOps & Infrastructure](#4-devops--infrastructure)
5. [AI-Assisted Development Workflow](#5-ai-assisted-development-workflow)
6. [Project History](#6-project-history)
7. [Getting Started](#7-getting-started)

---

## 1. What This Is

The IIoT Fleet Telemetry System is a real-time industrial asset monitoring platform. It tracks
10,000+ vehicles simultaneously, streaming live telemetry — position, fuel, speed, temperature,
engine health, cargo load — to a web dashboard at up to 2 updates/second per vehicle.

**Problem it solves:** an industrial fleet operator needs immediate visibility into where every
vehicle is, whether it's healthy, and when it crosses into a warning or danger state, without
waiting on batch reports or polling. The dashboard surfaces that as a live map, a searchable/
filterable vehicle list, per-vehicle telemetry gauges and event logs, and toast/notification
alerts on threshold breaches.

**Who it's for:** industrial fleet operators (the primary dashboard user), and — because this
repo doubles as a reference build — backend engineers evaluating a SignalR/MessagePack
real-time pipeline, frontend engineers evaluating 10k-item virtualized UI patterns, and system
architects evaluating an AI-agent-driven sprint workflow (see [§5](#5-ai-assisted-development-workflow)).

Full functional and non-functional requirements, business rules, data model, and API contract
live in [`docs/requirements/REQUIREMENTS.md`](requirements/REQUIREMENTS.md) — this section is a
one-paragraph summary of it, not a replacement.

---

## 2. Architecture

### The four services

| Service | Tech | Role |
|---|---|---|
| `frontend` | Next.js 15 (App Router) + TypeScript | Browser dashboard: map, sidebar, detail panel, notifications. Also hosts REST route handlers under `app/api/` (see [`frontend/AGENTS.md`](../frontend/AGENTS.md)). |
| `backend` | ASP.NET Core 8 Web API | REST endpoints (`/api/vehicles*`, `/api/telemetry/ingest`, `/api/health/signalr`), the `/fleethub` SignalR hub, and the background services that simulate or ingest/persist/broadcast telemetry (see [`backend/AGENTS.md`](../backend/AGENTS.md)). |
| `db` | PostgreSQL 16 (tuned image) | Persists `vehicles`, `telemetry_snapshots`, `vehicle_logs` via EF Core migrations. |
| `iiot-emitter` | Python 3.12 (`asyncio` + `aiohttp`) | Simulates up to 10,000 independent onboard IIoT devices, each posting its own telemetry tick to the backend over HTTP. Outbound-only; no inbound ports. |

### Data flow

```
 iiot-emitter                 backend                      frontend
 (10,000 async   ── POST ──►  ASP.NET Core 8               Next.js 15
  vehicle tasks)   /api/       ├─ TelemetryIngestController  (browser)
                    telemetry/    → validates, computes
                    ingest         status, upserts
                                   ILiveTelemetryStore,
                                   enqueues a write
                               ├─ TelemetryPersistenceService
                               │    (buffered batch writer)
                               │        │
                               │        ▼
                               │       db (PostgreSQL 16)
                               │  vehicles / telemetry_snapshots
                               │  / vehicle_logs
                               └─ LiveBroadcastService
                                    (~500ms tick)  ── SignalR ──►  /fleethub
                                                      MessagePack   (ReceiveFleetUpdate)
```

`frontend` also calls `backend`'s REST endpoints directly (`GET /api/vehicles`,
`GET /api/vehicles/{id}`, `GET /api/vehicles/{id}/logs`) for initial load and detail views. When
`USE_LIVE_TELEMETRY=false` (default outside Docker), the ingest pipeline above is replaced by an
in-memory `TelemetrySimulationService` on the backend — no `db` or `iiot-emitter` involvement;
see [§3](#3-key-design-decisions).

### Tech stack

| Layer | Technology |
|---|---|
| Frontend | Next.js 15 (App Router) + TypeScript + Tailwind CSS + Zustand + `@microsoft/signalr` + `@tanstack/react-virtual` |
| Backend | ASP.NET Core 8 Web API + SignalR + MessagePack + Entity Framework Core |
| Database | PostgreSQL 16 |
| Emitter | Python 3.12 + `asyncio` + `aiohttp` |
| DevOps | Docker + Docker Compose + GitHub Actions |
| AI Tools | Claude Code, GitHub Copilot, Gemini |

For full stack versions, directory maps, coding conventions, and API/data contracts, see the
root [`AGENTS.md`](../AGENTS.md), [`frontend/AGENTS.md`](../frontend/AGENTS.md), and
[`backend/AGENTS.md`](../backend/AGENTS.md) — those files are the source of truth this section
summarizes.

---

## 3. Key Design Decisions

**Live vs. dummy telemetry toggle (`USE_LIVE_TELEMETRY`).** The backend can source vehicle state
two ways: a legacy in-memory `TelemetrySimulationService` (random-walk simulation, no DB, no
HTTP — used by default for local `dotnet run`), or the live ingestion pipeline fed by
`iiot-emitter` and persisted to PostgreSQL (`USE_LIVE_TELEMETRY=true`, the Docker Compose
default). Both paths produce byte-identical `ApiVehicle` response shapes, so the frontend needs
zero changes regardless of which is active. This lets local development stay dependency-free
while the containerized stack demonstrates the real pipeline.

**Buffered/batched ingest pipeline, not synchronous writes.** At up to 10,000 vehicles ticking
independently, writing every telemetry POST straight to Postgres would spike to thousands of
concurrent DB connections — ruled out outright. Instead, `TelemetryIngestController` validates
and upserts an in-memory store, then enqueues onto a bounded channel that a single background
service drains in timed/sized batches, decoupling emitter cardinality from DB connection count
and keeping the read/broadcast path immune to DB latency. Full rationale, alternatives
considered (synchronous writes; an external queue like Kafka), and known follow-ups (no
at-least-once persistence guarantee, load testing still outstanding) are in
[ADR-001](decisions/ADR-001-telemetry-ingestion-pipeline.md).

**MessagePack over SignalR.** The `/fleethub` hub negotiates the MessagePack protocol for
`ReceiveFleetUpdate` broadcasts instead of plain JSON, cutting payload size for the
partial-update batches sent to every connected client roughly every 500ms — meaningful at
10,000-vehicle scale, where JSON's per-field key overhead would otherwise dominate bandwidth.

**Virtualization strategy.** The frontend renders the 10,000-vehicle sidebar list with
`@tanstack/react-virtual` (only visible rows mount), holds live vehicle state in a
`useRef<Map<string, Vehicle>>` for O(1) SignalR-driven updates without triggering a re-render
per tick, and flushes to rendered `useState` at most once per animation frame. A default-on
"focused view" additionally caps the sidebar to 10 curated vehicles with a "show all" toggle,
so the full virtualized list is opt-in rather than the default render path. See
[`frontend/AGENTS.md`](../frontend/AGENTS.md)'s Performance Rules for the complete list.

---

## 4. DevOps & Infrastructure

**Docker Compose topology (4 services):** `db` → `backend` → `frontend` and `iiot-emitter` →
`backend`, all on the `iiot-fleet-net` network. `iiot-emitter` and `frontend` both depend on
`backend`'s healthcheck; `backend` depends on `db`'s. `USE_LIVE_TELEMETRY=true` is set on
`backend` in the committed `docker-compose.yml`, making the containerized stack live-by-default
even though bare `dotnet run` still defaults to the dummy simulation. `db` uses a custom
`postgres:16-alpine`-based image (`db/Dockerfile`) tuned for sustained batched-write throughput
(`max_connections`, `shared_buffers`, `work_mem`, etc.) rather than the stock image.

**CI:** a single GitHub Actions workflow, [`.github/workflows/docker-image.yml`](../.github/workflows/docker-image.yml),
builds the Docker image on push/PR to `main`. A fix for a previously-failing build
(`claude/fix-docker-image-ci-workflow`) shipped standalone ahead of this document and is not yet
merged to `main` — see [`docs/sprints/BACKLOG.md`](sprints/BACKLOG.md) for current status.

**Full detail — quickstart, per-service breakdown, the complete environment variable reference,
switching back to dummy mode, scaling the emitter down, and a troubleshooting section (emitter
can't reach backend, FK violations, `db` unreachable from siblings, missing frontend `public/`)
— all live in [`DOCKER_README.md`](../DOCKER_README.md).** This section intentionally does not
duplicate those tables.

---

## 5. AI-Assisted Development Workflow

Every sprint in this repo's history (see [§6](#6-project-history)) was built by Claude Code
agents operating under a written protocol, not ad hoc prompting. This is a real, load-bearing
part of the project's design, documented in full in the root [`AGENTS.md`](../AGENTS.md).

**Agent roles** — each with a defined read/write scope enforced by convention:

| Role | Code | Owns |
|---|---|---|
| System designer | ARCH | `docs/**`, `AGENTS.md`, `README.md`, `CHANGELOG.md` — sprint files, requirements, changelog. Never writes application code. |
| Next.js engineer | NEXT | `frontend/**` — components, Zustand stores, SignalR client, route handlers. |
| .NET Core engineer | ASP.NET | `backend/**` — controllers, SignalR hub, MessagePack models, EF Core, background services. |
| Infrastructure engineer | INFRA | `docker-compose.yml`, both `Dockerfile`s, `.github/workflows/**`, `.env*`, `iiot-emitter/**`. |
| Quality analyst | QA | Test files and sprint acceptance-criteria updates only; verifies type-check/lint/build/tests and reports failures with file:line references. |
| Analyst | ANALYST | Performance/telemetry metrics analysis (SignalR latency, status distribution, alert counts). |

**Skills** (`.claude/skills/`, activated contextually): `sprint` (sprint authoring protocol +
caveman token rules), `nextjs`, `asp-dot-net-core`, `postgre-sql`, `devops`, and `caveman`
(a token-compressed communication mode used during sprint execution).

**The sprint loop.** Each sprint is one git branch, cut fresh from `origin/main`
(`claude/sprint-NN-<slug>`), authored by ARCH from
[`docs/sprints/archive/TEMPLATE.md`](sprints/archive/TEMPLATE.md) with every task fully
specified — context, files to read/modify/create, an explicit "do NOT touch" list, a sub-task
breakdown, and a verification command. Agents execute strictly task-by-task: read the sprint
file and relevant subsystem `AGENTS.md`, implement one task, run its verification command,
flip its status from `[ ]` to `[x]` only once verification passes, then commit with
`IIOT-S{NN}-{TASK-ID}: <one-line summary>` — one commit per task, never a batched commit for
the whole sprint. At sprint end, ARCH updates `CHANGELOG.md` (one `## vX.Y.Z` entry for the
whole sprint), bumps the version, and archives the sprint file to `docs/sprints/archive/`. This
document (`docs/PROJECT_OVERVIEW.md`) is itself the deliverable of one such task, `ARCH-009` in
[Sprint 05](sprints/BACKLOG.md).

---

## 6. Project History

**[Sprint 01 — Infrastructure & Application Setup](sprints/archive/sprint-01.md)** (2026-06-29 to
2026-07-06). Goal: take the backend from a pure in-memory demo to a persisted, documented API —
add Swagger/OpenAPI, wire PostgreSQL via EF Core (schema + seed), and confirm the Next.js
frontend type-checks against the real API contract. Shipped Swagger UI, the `FleetDbContext` +
initial migration, DB-backed vehicle metadata reads, and a PostgreSQL service in Docker Compose,
without removing the existing in-memory simulation.

**[Sprint 02 — Live IIoT Telemetry Ingestion](sprints/sprint-02.md)** (2026-07-09 to
2026-07-16). Goal: replace the in-memory dummy simulation with a real ingestion pipeline — a
Python emitter simulating 10,000 IIoT devices posting telemetry over HTTP into PostgreSQL,
rebroadcast live over the existing SignalR hub. Shipped the `USE_LIVE_TELEMETRY` toggle, the
buffered/batched persistence pipeline (see [ADR-001](decisions/ADR-001-telemetry-ingestion-pipeline.md)),
the `iiot-emitter` service and its tuned Postgres image, and a full `DOCKER_README.md` rewrite.
All 9 tasks shipped as `v0.2.0`; this sprint file has not yet been moved into
`docs/sprints/archive/` despite being fully complete — a known housekeeping gap, not an
indication of unfinished work.

**[Sprint 03 — Telemetry Reliability & Storage Hardening](sprints/archive/sprint-03.md)**
(2026-07-13 to 2026-07-20). First of three themed sprints split from a larger 9-task operator
brief (see [`docs/sprints/BACKLOG.md`](sprints/BACKLOG.md)). Goal: give operators visibility into
SignalR connection health, a client-side "inactive vehicle" concept distinct from the `offline`
status, and a bounded-growth telemetry retention policy. Shipped a connection-status indicator
backed by `GET /api/health/signalr`, 60-second-stationary inactive detection with a
`hideInactive` toggle, and `TelemetryRetentionService` (closing ADR-001's action item #5).
Shipped as `v0.3.0`.

**[Sprint 04 — Vehicle Editing, Recency Search, and Focused View](sprints/archive/sprint-04.md)**
(2026-07-13 to 2026-07-20). Second of the three themed sprints. Goal: let operators rename a
vehicle's fleet number/driver from the dashboard, fix dummy-mode vehicle IDs, bound search
results to recently-active vehicles, default the sidebar to a curated top-10 view, and fix
responsive/overflow issues. Shipped `PATCH /api/vehicles/{id}` (backed by a new
`display_number` column), a fix for `TelemetrySimulationService.MakeId()` generating
non-`VEH-NNNNN` gibberish IDs in dummy mode, a 24h-activity search filter, a default-on focused
view, and a responsive audit across `Header`/`Sidebar`/`MapView`/`DetailPanel`. QA's first
verification pass caught a real sprint-blocking bug — PATCH edits silently clobbered by the next
live-ingestion tick — fixed ad hoc before the sprint closed. All 12 tasks shipped as `v0.4.0`.

**Sprint 05 — Project Documentation** (this sprint, 2026-07-13 to 2026-07-16). Third and final
themed sprint from the same operator brief. Goal: this document — a single onboarding read
covering architecture, DevOps, the AI-assisted workflow, and project history, linked from
`README.md`. See [`docs/sprints/BACKLOG.md`](sprints/BACKLOG.md) for the full 9-task brief and
how it was split across Sprints 03–05.

---

## 7. Getting Started

For the fastest path to a running stack (all 4 services via Docker Compose, including live
telemetry), follow the **Quick start** section of [`DOCKER_README.md`](../DOCKER_README.md).

For running the frontend or backend individually against a local PostgreSQL install instead of
Docker, follow the **Local Dev Setup** section (Prerequisites, Frontend, Backend, Full Stack
commands) in the root [`AGENTS.md`](../AGENTS.md#local-dev-setup).

Both are kept current as the authoritative commands; this section intentionally does not
duplicate them.
