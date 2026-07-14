# Sprint 06 — SDD Workflow Docs, Compose Storage, and Helm Chart

---

## Note (Operator Prompt)

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S06 |
| **Branch** | `claude/sprint-06-sdd-docker-storage-helm` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-14 |
| **End date** | 2026-07-21 |
| **Goal** | Operators can deploy the IIoT Fleet Telemetry stack to Kubernetes via a documented Helm chart with real persistent storage, run the local Docker Compose stack with explicit persistent volumes and networks, and every future sprint is authored against a written-down Spec-Driven Development (SDD) workflow instead of tribal knowledge. |
| **Success metric** | `helm lint helm/iiot-fleet-app` and `helm template helm/iiot-fleet-app` both succeed with zero errors; `docker-compose up --build -d` starts all four services healthy on the explicit `iiot-fleet-net` network with the `postgres_data` volume present; `docs/SDD_WORKFLOW.md` and `docs/HELM_GUIDE.md` exist and are linked from `README.md`. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) via Docker Compose, plus a local Kubernetes cluster (kind/minikube) for Helm verification |
| **Agents involved** | ARCH, INFRA, QA |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

The 2026-07-14 operator brief asked for three things, none of which touch application code: (1) a written explanation of the Spec-Driven Development (SDD) methodology this project already practices informally via `REQUIREMENTS.md` → sprint files → task execution → QA verification → `CHANGELOG.md`, (2) explicit, documented persistent storage and network configuration in `docker-compose.yml` (today it has exactly one named volume, `postgres_data`, and an implicit default network only renamed via `networks.default.name`), and (3) a Helm chart — scaffolded the way `helm create iiot-fleet-app` would — that deploys the same four-service stack (`db`, `backend`, `frontend`, `iiot-emitter`) to Kubernetes, plus a deployment guide covering install, configuration, troubleshooting, and connecting to a cluster. This sprint delivers all three as documentation and infrastructure scope only; no `frontend/**` or `backend/**` source file is touched.

**Related documents:**
- `docs/requirements/REQUIREMENTS.md` — section 9 (Environment Variables) is the source of truth for every env var carried into the Helm chart's `values.yaml`
- `DOCKER_README.md` — existing Compose documentation style/structure to mirror for `docs/HELM_GUIDE.md`
- `docs/sprints/archive/TEMPLATE.md` — the spec-to-task template `docs/SDD_WORKFLOW.md` must describe accurately

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-06-sdd-docker-storage-helm origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

**Branch:**
- [ ] Branch `claude/sprint-06-sdd-docker-storage-helm` exists and is clean (`git status` shows no uncommitted changes)
- [ ] Branch was cut from `origin/main`

**Frontend:** N/A — this sprint does not modify any file under `frontend/**` (only `frontend/package.json`'s version field, at sprint-end only).

**Backend:** N/A — this sprint does not modify any file under `backend/**`.

**Database:** N/A — no schema change; the Helm chart's PostgreSQL StatefulSet is a new deployment target, not a migration.

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] Active sprint file (`docs/sprints/sprint-06.md`, this file) read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full
- [ ] `DOCKER_README.md` read in full (needed by INFRA-004, INFRA-005, ARCH-012)

**Sprint-specific:**
- [ ] Docker Compose v2 available (`docker-compose version` or `docker compose version`)
- [ ] Helm 3.x available (`helm version` reports `v3.x`) — required for INFRA-005 through QA-006
- [ ] (Optional, best-effort only) a local Kubernetes cluster (kind or minikube) available for QA-006's `helm install` check

---

## Task Index (Top-Level Todo)

- [x] ARCH-011 — Author SDD (Spec-Driven Development) Workflow Documentation
- [x] INFRA-004 — Add Explicit Network to Docker Compose
- [x] INFRA-005 — Scaffold Helm Chart Skeleton + PostgreSQL StatefulSet/PVC
- [x] INFRA-006 — Add Helm Templates for Backend and Frontend Deployments
- [ ] INFRA-007 — Add Helm Templates for iiot-emitter and Ingress
- [ ] ARCH-012 — Write Helm Deployment Guide Documentation
- [ ] QA-006 — Verify Docker Compose and Helm Chart End-to-End
- [ ] ARCH-013 — Sprint-End — CHANGELOG, Version Bump, Archive

---

## Dependency Map

```
ARCH-011 (no deps)   INFRA-004 (no deps)   INFRA-005 (no deps)
                                                    │
                                                    ▼
                                              INFRA-006
                                                    │
                                                    ▼
                                              INFRA-007
        │                    │                     │
        └────────────────────┼─────────────────────┘
                              ▼
                        ARCH-012 (needs INFRA-005, 006, 007)
        │                    │                     │
        └────────────────────┼─────────────────────┘
     (ARCH-011, INFRA-004 also feed in)
                              ▼
                           QA-006
                              │
                              ▼
                          ARCH-013
```

---

## Tasks

---

### ARCH-011: Author SDD (Spec-Driven Development) Workflow Documentation

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

This project already practices Spec-Driven Development informally: `docs/requirements/REQUIREMENTS.md` is the spec, `docs/sprints/archive/TEMPLATE.md` turns spec slices into executable sprint files, `.claude/skills/sprint/SKILL.md` governs sprint authoring, and `AGENTS.md`'s Sprint Loop governs execution — but no single document explains this loop end-to-end to a new contributor or stakeholder. This task creates `docs/SDD_WORKFLOW.md` documenting the methodology, links it from `README.md` and `AGENTS.md`, and registers `helm/**` as a new INFRA write-scope path in `AGENTS.md` since INFRA-005 onward introduces a `helm/` directory that `AGENTS.md`'s current INFRA write-scope row does not list.

---

**Files to read before starting:**

- `AGENTS.md` — current ARCH/INFRA write scopes and File Contracts table format, to extend correctly
- `docs/requirements/REQUIREMENTS.md` — the spec source of truth the SDD doc must reference (not restate)
- `docs/sprints/archive/TEMPLATE.md` — the spec-to-task template the SDD doc must describe accurately
- `.claude/skills/sprint/SKILL.md` — sprint authoring protocol to summarize
- `README.md` — current structure, to find where to add the doc link

---

**Files to modify:**

- `AGENTS.md` — add `helm/**` to the INFRA write-scope cell; add a `helm/iiot-fleet-app/**` row to the File Contracts table; add `docs/SDD_WORKFLOW.md` to the Key Knowledge Base Documents table
- `README.md` — add a link to `docs/SDD_WORKFLOW.md`

---

**Files to create:**

- `docs/SDD_WORKFLOW.md` — explains the SDD loop used in this repo: `REQUIREMENTS.md` (spec) → `sprint` skill authors `docs/sprints/sprint-NN.md` → agent executes tasks top-to-bottom per the dependency map → QA verifies acceptance criteria → ARCH runs the Sprint-End Checklist (`CHANGELOG.md`, version bump, archive) → repeat. Includes a text diagram of the loop, a role table (ARCH/INFRA/NEXT/ASP.NET/QA/ANALYST), and a "how to start a new sprint" section pointing at the `sprint` skill.

---

**Do NOT touch:**

- `backend/**`, `frontend/**` — this task is documentation-only
- `docker-compose.yml` — owned by INFRA-004
- `helm/**` — does not exist yet; owned by INFRA-005 onward

---

**Sub-task breakdown:**

- [ ] Read all five files listed above
- [ ] Draft `docs/SDD_WORKFLOW.md`: what SDD means in this repo, the spec→sprint→task→verify→changelog loop with a text diagram, a role table, and how to start a new sprint
- [ ] Add `helm/**` to `AGENTS.md`'s INFRA write-scope cell and a new File Contracts row for `helm/iiot-fleet-app/**`
- [ ] Link `docs/SDD_WORKFLOW.md` from `README.md` and from `AGENTS.md`'s Key Knowledge Base Documents table

---

**Implementation notes:**

1. Do not restate `REQUIREMENTS.md` content verbatim — `SDD_WORKFLOW.md` documents *process*, not the product spec itself.
2. Match the repo's existing documentation style: tables over prose walls, as seen throughout `AGENTS.md`.
3. In `AGENTS.md`'s INFRA row, append `helm/**` to the existing Write scope cell (`docker-compose.yml, backend/Dockerfile, frontend/Dockerfile, .github/workflows/**, .env* files, iiot-emitter/**`) — do not remove any existing entry.
4. Add this File Contracts row: `helm/iiot-fleet-app/**` | INFRA | Chart values must never hardcode real secrets; passwords are placeholder defaults in `values.yaml`, overridden at install time.

---

**Acceptance criteria:**

1. `docs/SDD_WORKFLOW.md` exists and documents the spec→sprint→task→verify→changelog loop with no `{{PLACEHOLDER}}` text remaining
2. `AGENTS.md`'s INFRA write-scope row includes `helm/**`
3. `AGENTS.md`'s File Contracts table has a new row for `helm/iiot-fleet-app/**`
4. `README.md` links to `docs/SDD_WORKFLOW.md`
5. No file outside `AGENTS.md`, `README.md`, `docs/SDD_WORKFLOW.md` is modified by this task

---

**Verification command:**

```bash
test -f docs/SDD_WORKFLOW.md && echo "SDD doc exists"
grep -q "helm/\*\*" AGENTS.md && echo "INFRA scope updated"
grep -q "SDD_WORKFLOW.md" README.md && echo "README linked"
git diff --stat   # expect only AGENTS.md, README.md, docs/SDD_WORKFLOW.md
```

---

**Rollback:**

```bash
git rm docs/SDD_WORKFLOW.md
git checkout -- AGENTS.md README.md
```

---

### INFRA-004: Add Explicit Network to Docker Compose

**Agent:** INFRA
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`docker-compose.yml` currently defines a single named volume (`postgres_data`) and relies on Compose's implicit default network, only renamed via `networks.default.name: iiot-fleet-net` with no explicit driver or per-service `networks:` list. This task makes the network an explicit top-level bridge network attached by name on all four services. No second volume is added — `postgres_data` remains the only named volume.

---

**Files to read before starting:**

- `docker-compose.yml` — current services/volumes/networks structure
- `DOCKER_README.md` — currently documented volume/network behavior, to keep the new docs consistent with existing sections

---

**Files to modify:**

- `docker-compose.yml` — replace the `networks.default.name` shorthand with an explicit top-level `networks.iiot-fleet-net` (`driver: bridge`) block; add `networks: [iiot-fleet-net]` to `db`, `backend`, `frontend`, `iiot-emitter`
- `DOCKER_README.md` — document the explicit network block

---

**Files to create:** None

---

**Do NOT touch:**

- `backend/**`, `frontend/**`, `db/Dockerfile`, `db/postgresql.conf`
- `helm/**` — does not exist yet at this task's execution time

---

**Sub-task breakdown:**

- [ ] Add top-level `networks: iiot-fleet-net: driver: bridge`, removing the old `networks: default: name: iiot-fleet-net` shorthand
- [ ] Add `networks: - iiot-fleet-net` under each of `db`, `backend`, `frontend`, `iiot-emitter`
- [ ] Update `DOCKER_README.md` with the explicit network section
- [ ] Run `docker-compose config` to confirm the file still parses cleanly

---

**Implementation notes:**

1. Keep the volume name `postgres_data` unchanged — it's referenced by name in `DOCKER_README.md`'s "Stop and wipe volumes" section, and no second volume is introduced by this task.
2. Service names (`db`, `backend`, `frontend`, `iiot-emitter`) must not change — `AGENTS.md`'s File Contracts table pins these.
3. Do not add a network alias or change any port mapping — this task is additive only.

---

**Acceptance criteria:**

1. `docker-compose config` exits `0` with no warnings about unknown top-level keys
2. All four services list `iiot-fleet-net` under `networks:`
3. Top-level `volumes:` still contains only `postgres_data`
4. `docker-compose up --build -d` starts all four containers, with `db`/`backend`/`frontend` reaching `Up (healthy)`
5. `DOCKER_README.md` documents the explicit network block

---

**Verification command:**

```bash
docker-compose config --quiet && echo "compose file valid"
docker-compose up --build -d
docker-compose ps   # expect db, backend, frontend "Up (healthy)"; iiot-emitter "Up"
docker-compose down
```

---

**Rollback:**

```bash
git checkout -- docker-compose.yml DOCKER_README.md
```

---

### INFRA-005: Scaffold Helm Chart Skeleton + PostgreSQL StatefulSet/PVC

**Agent:** INFRA
**Depends on:** NONE
**Status:** [x]

---

**Context:**

No Helm chart exists in the repo. This task creates the chart skeleton equivalent to `helm create iiot-fleet-app` (`Chart.yaml`, `values.yaml`, `templates/_helpers.tpl`, `templates/NOTES.txt`, `.helmignore`) under `helm/iiot-fleet-app/`, then adds the first workload: a PostgreSQL `StatefulSet` using `volumeClaimTemplates` for persistent storage, a headless `Service` for stable DNS, and a `Secret` template for `POSTGRES_PASSWORD`. This is the Kubernetes-native equivalent of `docker-compose.yml`'s `db` service and its `postgres_data` volume.

---

**Files to read before starting:**

- `docker-compose.yml` — `db` service env vars, build context, and healthcheck, to translate 1:1 into k8s
- `db/Dockerfile`, `db/postgresql.conf` — confirms the image is custom-built, not a stock public image; the chart must reference a buildable image name, not assume a public registry
- `docs/requirements/REQUIREMENTS.md` section 9 — env var reference for the `ConnectionStrings__Fleet` format wired in INFRA-006

---

**Files to modify:** None (net-new directory)

---

**Files to create:**

- `helm/iiot-fleet-app/Chart.yaml` — `name: iiot-fleet-app`, `version: 0.1.0`, `appVersion` matching `frontend/package.json`
- `helm/iiot-fleet-app/values.yaml` — top-level `db.*` values (image repository/tag, storage size, password placeholder)
- `helm/iiot-fleet-app/.helmignore`
- `helm/iiot-fleet-app/templates/_helpers.tpl` — standard `name`/`fullname`/`labels` helpers
- `helm/iiot-fleet-app/templates/NOTES.txt` — post-install usage hints
- `helm/iiot-fleet-app/templates/db-statefulset.yaml`
- `helm/iiot-fleet-app/templates/db-service.yaml` — headless (`clusterIP: None`), port 5432
- `helm/iiot-fleet-app/templates/db-secret.yaml` — `POSTGRES_PASSWORD` sourced from `values.db.password`, base64-encoded via Helm's `b64enc`

---

**Do NOT touch:**

- `docker-compose.yml`, `backend/**`, `frontend/**`, `db/Dockerfile`, `db/postgresql.conf`

---

**Sub-task breakdown:**

- [ ] Create `Chart.yaml`, `values.yaml`, `.helmignore`, `templates/_helpers.tpl`, `templates/NOTES.txt`
- [ ] Create `templates/db-secret.yaml` sourcing the password from `values.db.password`
- [ ] Create `templates/db-statefulset.yaml` using `volumeClaimTemplates` (no separate PVC manifest — StatefulSet-managed per-pod PVCs are the idiomatic k8s pattern)
- [ ] Create `templates/db-service.yaml` (headless, `clusterIP: None`, required for StatefulSet DNS)
- [ ] Run `helm lint helm/iiot-fleet-app` and `helm template helm/iiot-fleet-app` to confirm the chart renders

---

**Implementation notes:**

1. `Chart.yaml`: `name: iiot-fleet-app`, `version: 0.1.0`; set `appVersion` to match the current `frontend/package.json` version at the time this task runs.
2. Use `volumeClaimTemplates` inside the StatefulSet, not a standalone `PersistentVolumeClaim` — this is what "storage and volume configurations" means in a Helm/k8s context, distinct from Compose's single shared named volume.
3. `values.yaml` must expose: `db.image.repository`, `db.image.tag`, `db.storage.size` (default `1Gi`), `db.password` (default placeholder, e.g. `"changeme"`, with a comment instructing operators to override via `--set db.password=...` or a values override file — never commit a real password).
4. `db.image.repository` defaults to a placeholder like `iiot-fleet-db`, since `db/Dockerfile`'s custom-tuned Postgres image is not published to any public registry — `templates/NOTES.txt` must instruct operators to `docker build -t iiot-fleet-db ./db` and push (or `kind load`) it before `helm install`.
5. The headless service's name must be predictable — `{{ include "iiot-fleet-app.fullname" . }}-db` — since INFRA-006's backend `ConnectionStrings__Fleet` must resolve `db` via this exact DNS name.

---

**Acceptance criteria:**

1. `helm lint helm/iiot-fleet-app` passes with 0 errors
2. `helm template helm/iiot-fleet-app` renders a `StatefulSet` with a `volumeClaimTemplates` entry, a headless `Service` (`clusterIP: None`), and a `Secret` — no template errors
3. No real password or secret value appears anywhere in `helm/iiot-fleet-app/**`
4. `Chart.yaml`'s `appVersion` matches `frontend/package.json`'s `version` field

---

**Verification command:**

```bash
helm lint helm/iiot-fleet-app
helm template test-release helm/iiot-fleet-app | grep -E "kind: (StatefulSet|Service|Secret)"
helm template test-release helm/iiot-fleet-app | grep -i "changeme"   # confirm only the placeholder appears
```

---

**Rollback:**

```bash
git rm -r helm/iiot-fleet-app
```

---

### INFRA-006: Add Helm Templates for Backend and Frontend Deployments

**Agent:** INFRA
**Depends on:** INFRA-005
**Status:** [x]

---

**Context:**

With the chart skeleton and `db` `StatefulSet` in place, this task adds `Deployment` + `Service` templates for `backend` (ASP.NET Core, port 8080) and `frontend` (Next.js, port 3000), mirroring `docker-compose.yml`'s `backend`/`frontend` service definitions (env vars, health checks) as k8s `Deployments` with readiness/liveness probes and `ClusterIP` `Services`. A `ConfigMap` centralizes non-secret env vars (`FRONTEND_ORIGIN`, `ASPNETCORE_ENVIRONMENT`, `NEXT_PUBLIC_API_URL`, `USE_LIVE_TELEMETRY`) so they're editable without touching templates.

---

**Files to read before starting:**

- `docker-compose.yml` — `backend`/`frontend` env vars, ports, and healthcheck definitions to translate
- `helm/iiot-fleet-app/values.yaml`, `templates/_helpers.tpl`, `templates/db-service.yaml` — established chart conventions and the `db` Service DNS name to wire into the backend's connection string
- `backend/Dockerfile`, `frontend/Dockerfile` — exposed ports and entrypoints; confirm nothing extra is needed for k8s

---

**Files to modify:**

- `helm/iiot-fleet-app/values.yaml` — add `backend.*` and `frontend.*` value blocks (image, replicas, resource requests/limits)

---

**Files to create:**

- `helm/iiot-fleet-app/templates/backend-deployment.yaml`
- `helm/iiot-fleet-app/templates/backend-service.yaml` — `ClusterIP`, port 8080
- `helm/iiot-fleet-app/templates/frontend-deployment.yaml`
- `helm/iiot-fleet-app/templates/frontend-service.yaml` — `ClusterIP`, port 3000
- `helm/iiot-fleet-app/templates/app-configmap.yaml` — non-secret env vars

---

**Do NOT touch:**

- `helm/iiot-fleet-app/templates/db-*.yaml`, `docker-compose.yml`, `backend/**`, `frontend/**`

---

**Sub-task breakdown:**

- [ ] Add `backend.*` and `frontend.*` keys to `values.yaml` (image repository/tag, `replicaCount`, resource requests/limits)
- [ ] Create `app-configmap.yaml` with `FRONTEND_ORIGIN`, `ASPNETCORE_ENVIRONMENT`, `NEXT_PUBLIC_API_URL`, `USE_LIVE_TELEMETRY`
- [ ] Create `backend-deployment.yaml`: `tcpSocket` readiness/liveness probe on 8080, `envFrom` the `ConfigMap`, `ConnectionStrings__Fleet` built from the `db-secret` + `db-service` DNS
- [ ] Create `backend-service.yaml`
- [ ] Create `frontend-deployment.yaml`: `httpGet` readiness/liveness probe on `/` port 3000, `envFrom` the `ConfigMap`
- [ ] Create `frontend-service.yaml`
- [ ] `helm template` and confirm the backend Deployment's `ConnectionStrings__Fleet` env value resolves to the `db-service` hostname from INFRA-005

---

**Implementation notes:**

1. `NEXT_PUBLIC_API_URL` in the `ConfigMap` must point at the backend Service's in-cluster DNS name (`{{ include "iiot-fleet-app.fullname" . }}-backend:8080`) — same pattern `docker-compose.yml` uses with `http://backend:8080`, just k8s DNS instead of Compose DNS.
2. The backend's `ConnectionStrings__Fleet` env var must be assembled from a Secret-sourced password plus the `db-service` fullname host — do not put the assembled connection string in the `ConfigMap`, since it embeds a password.
3. Probes: the backend has no HTTP health endpoint (per `docker-compose.yml`'s comment — the `aspnet:8.0` image has neither `curl` nor `wget`), so use `tcpSocket: port: 8080` for both readiness and liveness, matching the Compose healthcheck's actual behavior. The frontend's Compose healthcheck uses `wget --spider http://localhost:3000/`; for k8s, use `httpGet: path: / port: 3000` instead — the kubelet probes natively and needs no `wget` inside the container.
4. `values.yaml`'s `backend.image.repository`/`frontend.image.repository` default to placeholders (`iiot-fleet-backend`, `iiot-fleet-frontend`); ARCH-012's `HELM_GUIDE.md` documents building and pushing them from `backend/Dockerfile` and `frontend/Dockerfile` before install.
5. Do not add an `Ingress` here — that is INFRA-007's scope.

---

**Acceptance criteria:**

1. `helm lint helm/iiot-fleet-app` passes with 0 errors
2. `helm template` renders `backend-deployment.yaml` with a `tcpSocket` probe on port 8080 and `frontend-deployment.yaml` with an `httpGet` probe on port 3000
3. The rendered backend `Deployment`'s `ConnectionStrings__Fleet` env value references the `db` `Secret` and the `db` `Service`'s fullname DNS host
4. The rendered frontend `Deployment`'s `NEXT_PUBLIC_API_URL` resolves to the backend `Service`'s in-cluster DNS name, not `localhost`

---

**Verification command:**

```bash
helm lint helm/iiot-fleet-app
helm template test-release helm/iiot-fleet-app | grep -A5 "kind: Deployment"
helm template test-release helm/iiot-fleet-app | grep "NEXT_PUBLIC_API_URL" -A1
helm template test-release helm/iiot-fleet-app | grep "ConnectionStrings__Fleet" -A1
```

---

**Rollback:**

```bash
git checkout -- helm/iiot-fleet-app/values.yaml
git rm helm/iiot-fleet-app/templates/backend-deployment.yaml \
       helm/iiot-fleet-app/templates/backend-service.yaml \
       helm/iiot-fleet-app/templates/frontend-deployment.yaml \
       helm/iiot-fleet-app/templates/frontend-service.yaml \
       helm/iiot-fleet-app/templates/app-configmap.yaml
```

---

### INFRA-007: Add Helm Templates for iiot-emitter and Ingress

**Agent:** INFRA
**Depends on:** INFRA-005, INFRA-006
**Status:** [ ]

---

**Context:**

`docker-compose.yml`'s fourth service, `iiot-emitter`, has no k8s equivalent yet. It's an outbound-only Python client with no inbound ports, so it maps to a `Deployment` with no matching `Service`. This task adds its `Deployment` template plus an optional `Ingress` exposing the frontend (and, if desired, the backend) outside the cluster — gated behind a `values.yaml` toggle, since not every k8s environment (e.g. a local kind/minikube cluster without an ingress controller) can use one.

---

**Files to read before starting:**

- `docker-compose.yml` — `iiot-emitter` env vars (`BACKEND_URL`, `VEHICLE_COUNT`, `TICK_INTERVAL_SECONDS`, `MAX_CONCURRENCY`) and its `depends_on: backend: condition: service_healthy` semantics
- `helm/iiot-fleet-app/templates/backend-deployment.yaml`, `backend-service.yaml` — the backend readiness probe and Service DNS name the emitter must target via `BACKEND_URL`

---

**Files to modify:**

- `helm/iiot-fleet-app/values.yaml` — add `emitter.*` block (image, env values, `replicaCount` fixed at 1) and `ingress.*` block (`enabled: false` by default, `host`, `className`)

---

**Files to create:**

- `helm/iiot-fleet-app/templates/emitter-deployment.yaml`
- `helm/iiot-fleet-app/templates/ingress.yaml` — wrapped in `{{- if .Values.ingress.enabled }}`

---

**Do NOT touch:**

- `helm/iiot-fleet-app/templates/db-*.yaml`, `helm/iiot-fleet-app/templates/backend-*.yaml`, `helm/iiot-fleet-app/templates/frontend-*.yaml`

---

**Sub-task breakdown:**

- [ ] Add `emitter.*` and `ingress.*` keys to `values.yaml` (`ingress.enabled` defaults to `false`)
- [ ] Create `emitter-deployment.yaml`: no `Service` (outbound-only, matching `iiot-emitter` having no `ports:` in `docker-compose.yml`); `BACKEND_URL` env pointing at the backend `Service` DNS; an `initContainer` that polls the backend before the main container starts, approximating Compose's `depends_on: service_healthy`
- [ ] Create `ingress.yaml` gated on `.Values.ingress.enabled`, routing `/` to the frontend `Service` and optionally `/api`, `/swagger`, `/fleethub` to the backend `Service`
- [ ] `helm template --set ingress.enabled=true --set ingress.host=<host>` and confirm the `Ingress` renders; confirm it's absent with default values

---

**Implementation notes:**

1. Kubernetes has no direct equivalent of Compose's `depends_on: condition: service_healthy` — a `Deployment`'s Pods start independently of other Pods. Approximate it with an `initContainer` on `emitter-deployment.yaml` that polls the backend Service (e.g. a busybox `wget`-loop) until it responds, before the main emitter container starts. Document this gap explicitly — do not claim it behaves identically to Compose's `depends_on`.
2. `emitter.replicaCount` must be fixed at `1` in `values.yaml`, with a comment explaining why: the emitter is not horizontally scalable — each replica independently ticks the *entire* fleet, so N replicas would multiply telemetry per vehicle N-fold. This is a hard constraint, not a tunable.
3. `Ingress`: use `networking.k8s.io/v1`, `pathType: Prefix`. Do not hardcode a default hostname — `ingress.host` defaults to `""`, and `templates/NOTES.txt` / `docs/HELM_GUIDE.md` (ARCH-012) must instruct operators to set it.
4. The emitter's `BACKEND_URL` must be `http://{{ include "iiot-fleet-app.fullname" . }}-backend:8080` — the same DNS pattern as the frontend's `NEXT_PUBLIC_API_URL` from INFRA-006.

---

**Acceptance criteria:**

1. `helm lint helm/iiot-fleet-app` passes with 0 errors
2. `helm template test-release helm/iiot-fleet-app` (default values) does NOT render an `Ingress` resource
3. `helm template test-release helm/iiot-fleet-app --set ingress.enabled=true --set ingress.host=fleet.example.local` renders exactly one `Ingress` routing to the frontend `Service`
4. `emitter-deployment.yaml` has no matching Service template (outbound-only, matching `docker-compose.yml`'s `iiot-emitter` having no `ports:`)
5. `values.yaml`'s `emitter.replicaCount` is `1` with an explanatory comment

---

**Verification command:**

```bash
helm lint helm/iiot-fleet-app
helm template test-release helm/iiot-fleet-app | grep -c "kind: Ingress"   # expect 0
helm template test-release helm/iiot-fleet-app --set ingress.enabled=true --set ingress.host=fleet.example.local | grep -c "kind: Ingress"   # expect 1
ls helm/iiot-fleet-app/templates/ | grep emitter   # expect only emitter-deployment.yaml
```

---

**Rollback:**

```bash
git checkout -- helm/iiot-fleet-app/values.yaml
git rm helm/iiot-fleet-app/templates/emitter-deployment.yaml helm/iiot-fleet-app/templates/ingress.yaml
```

---

### ARCH-012: Write Helm Deployment Guide Documentation

**Agent:** ARCH
**Depends on:** INFRA-005, INFRA-006, INFRA-007
**Status:** [ ]

---

**Context:**

The Helm chart built across INFRA-005 through INFRA-007 has no user-facing documentation. The operator brief explicitly requires install steps, configuration options, troubleshooting, and k8s connection examples — mirroring the depth of the existing `DOCKER_README.md` but for the Helm/Kubernetes path. This task authors `docs/HELM_GUIDE.md` and links it from `README.md` and `AGENTS.md`'s Key Knowledge Base Documents table.

---

**Files to read before starting:**

- `helm/iiot-fleet-app/Chart.yaml`, `values.yaml` — the final config surface to document
- `helm/iiot-fleet-app/templates/*.yaml` — every template from INFRA-005/006/007, to document accurately (especially the ingress toggle, the emitter's fixed `replicaCount: 1`, and the `initContainer` health-gate approximation)
- `DOCKER_README.md` — house style/structure to mirror (quick start, per-service breakdown, env var reference table, troubleshooting)
- `AGENTS.md` — Key Knowledge Base Documents table format

---

**Files to modify:**

- `README.md` — add a link to `docs/HELM_GUIDE.md`
- `AGENTS.md` — add a `docs/HELM_GUIDE.md` row to the Key Knowledge Base Documents table

---

**Files to create:**

- `docs/HELM_GUIDE.md` — sections: prerequisites (Helm 3.x, a k8s cluster, a kind/minikube example), building and loading/pushing the four custom images (db, backend, frontend, emitter), a `helm install` walkthrough, a full `values.yaml` reference table, `helm upgrade`/`helm uninstall`, connecting to the cluster (`kubectl port-forward` examples for frontend/backend/Swagger, since Ingress is opt-in), and troubleshooting (Pending PVC, `ImagePullBackOff` for unpublished custom images, emitter `CrashLoopBackOff` if the backend isn't ready, Ingress not routing when no ingress controller is installed)

---

**Do NOT touch:**

- `helm/iiot-fleet-app/**`, `docker-compose.yml`, `backend/**`, `frontend/**`

---

**Sub-task breakdown:**

- [ ] Read every file listed above
- [ ] Write prerequisites + quick-start sections (`helm install iiot-fleet-app ./helm/iiot-fleet-app`)
- [ ] Write the full `values.yaml` reference table (every key from INFRA-005/006/007, its default, and its description)
- [ ] Write a "connect to Kubernetes" section with `kubectl port-forward` examples for frontend (3000), backend (8080/`/swagger`), and one fully worked kind-cluster end-to-end example
- [ ] Write the troubleshooting section: Pending PVC (no default `StorageClass`), `ImagePullBackOff` (custom images not pushed/loaded), emitter `CrashLoopBackOff` (backend not ready — reference INFRA-007's `initContainer` gap), Ingress not routing (no ingress controller in cluster)
- [ ] Link `docs/HELM_GUIDE.md` from `README.md` and `AGENTS.md`

---

**Implementation notes:**

1. Explicitly document that the `db`/`backend`/`frontend`/`emitter` images are NOT published to any public registry — operators must `docker build` each and push to a registry their cluster can pull from (or `kind load docker-image` for local kind clusters). This is the single most likely first-run failure (`ImagePullBackOff`) and must be the first troubleshooting entry.
2. Cross-reference `DOCKER_README.md` rather than duplicating its per-service behavior explanations — link to it for "what does this service do", and keep `HELM_GUIDE.md` focused on the k8s-specific delta (StatefulSet storage, Ingress, probes, the `depends_on`-vs-`initContainer` gap).
3. Include one fully worked `kind` example end-to-end (create cluster → build + load 4 images → `helm install` → port-forward → `curl`) — this is the most reproducible way for a reader to verify the chart works without a cloud account.

---

**Acceptance criteria:**

1. `docs/HELM_GUIDE.md` exists with prerequisites, install, values reference, connect-to-k8s, and troubleshooting sections, with no placeholder text
2. Every key present in `helm/iiot-fleet-app/values.yaml` (as of INFRA-007) appears in the values reference table
3. `README.md` and `AGENTS.md` link to `docs/HELM_GUIDE.md`
4. The worked kind-cluster example is a copy-pasteable, correctly ordered command sequence

---

**Verification command:**

```bash
test -f docs/HELM_GUIDE.md && echo "Helm guide exists"
grep -q "HELM_GUIDE.md" README.md AGENTS.md && echo "linked"
```

---

**Rollback:**

```bash
git rm docs/HELM_GUIDE.md
git checkout -- README.md AGENTS.md
```

---

### QA-006: Verify Docker Compose and Helm Chart End-to-End

**Agent:** QA
**Depends on:** ARCH-011, INFRA-004, INFRA-005, INFRA-006, INFRA-007, ARCH-012
**Status:** [ ]

---

**Context:**

Verify everything in this sprint actually works together before sprint-end: the Docker Compose stack with the new network comes up healthy, the Helm chart lints and templates cleanly (and, if a local k8s cluster is available, actually installs), and both new docs are internally consistent with the artifacts they describe.

---

**Files to read before starting:**

- Every file created or modified by ARCH-011, INFRA-004, INFRA-005, INFRA-006, INFRA-007, and ARCH-012 (this sprint's full diff)

---

**Files to modify:**

- This sprint file's `Status`/`Task Index` checkboxes only

---

**Files to create:** None

---

**Do NOT touch:**

- Any production source file (per QA's `AGENTS.md` write scope)

---

**Sub-task breakdown:**

- [ ] Run the Docker Compose verification commands from INFRA-004
- [ ] Run the `helm lint` + `helm template` verification commands from INFRA-005, INFRA-006, INFRA-007
- [ ] If a local cluster is available (kind/minikube), run `helm install --dry-run` at minimum; run a real `helm install` + `kubectl get pods` if the environment permits
- [ ] Cross-check `docs/SDD_WORKFLOW.md` and `docs/HELM_GUIDE.md` against the actual file contents for accuracy (no stale paths, no leftover placeholder text)
- [ ] Report any failing acceptance criterion with exact command output and file:line

---

**Implementation notes:**

1. If no k8s cluster is available in the execution environment, `helm lint` + `helm template` are the mandatory minimum bar — `helm install` is best-effort, and its absence must be reported explicitly, not silently skipped.
2. Report using the same format as prior QA tasks (see `docs/sprints/archive/sprint-04.md`'s `QA-003` for style reference).

---

**Acceptance criteria:**

1. All acceptance criteria from ARCH-011, INFRA-004, INFRA-005, INFRA-006, INFRA-007, and ARCH-012 are independently confirmed TRUE
2. `docker-compose up --build -d` then `docker-compose ps` shows `db`/`backend`/`frontend` as `Up (healthy)`
3. `helm lint helm/iiot-fleet-app` passes with 0 errors
4. The final report lists any environment-limited checks that could not run (e.g. no k8s cluster available) rather than marking them silently passed

---

**Verification command:**

```bash
docker-compose up --build -d && docker-compose ps
helm lint helm/iiot-fleet-app
helm template test-release helm/iiot-fleet-app >/dev/null && echo "chart renders"
docker-compose down
```

---

**Rollback:** N/A — verification-only task, no source changes to roll back.

---

### ARCH-013: Sprint-End — CHANGELOG, Version Bump, Archive

**Agent:** ARCH
**Depends on:** QA-006
**Status:** [ ]

---

**Context:**

All prior tasks are `[x]` and QA-006 has confirmed the sprint works end-to-end. This task closes out the sprint per the Sprint-End Checklist: bump `frontend/package.json`'s version, add a `CHANGELOG.md` entry, open a PR, archive the sprint file, and update `AGENTS.md`'s Current Sprint pointer.

---

**Files to read before starting:**

- `CHANGELOG.md`, `frontend/package.json`
- `docs/sprints/archive/sprint-05.md`'s `ARCH-010` task — style reference for this task type

---

**Files to modify:**

- `frontend/package.json` (version bump)
- `CHANGELOG.md`
- `AGENTS.md` (Current Sprint section)
- This sprint file (moved to archive)

---

**Files to create:** None

---

**Do NOT touch:**

- Any application source file

---

**Sub-task breakdown:**

- [ ] Bump `frontend/package.json` version `0.5.0` → `0.6.0` (minor bump — new user-facing deployment capability, not a patch)
- [ ] Add `## v0.6.0 — 2026-07-21` entry to `CHANGELOG.md` with `### Add` (Helm chart, `docs/SDD_WORKFLOW.md`, `docs/HELM_GUIDE.md`, Compose explicit network) sections
- [ ] Confirm `CHANGELOG.md`'s top version matches `frontend/package.json`
- [ ] Move `docs/sprints/sprint-06.md` → `docs/sprints/archive/sprint-06.md`
- [ ] Update `AGENTS.md`'s `## Current Sprint` to reflect sprint 06 as delivered, no active sprint
- [ ] Open PR: `claude/sprint-06-sdd-docker-storage-helm` → `main`

---

**Implementation notes:**

1. Follow the exact `CHANGELOG.md` section format used by the `v0.5.0` entry (`### Add` / `### Fix` / `### Update` headers).
2. This sprint adds zero backend/frontend application code — the `CHANGELOG.md` entry should be framed as infrastructure/documentation/deployment capability, not a dashboard feature change.

---

**Acceptance criteria:**

1. `frontend/package.json` version bumped to `0.6.0` and matches `CHANGELOG.md`'s top entry
2. `docs/sprints/archive/sprint-06.md` exists; `docs/sprints/sprint-06.md` no longer exists
3. `AGENTS.md`'s Current Sprint section reflects sprint 06 as delivered
4. PR opened targeting `main`

---

**Verification command:**

```bash
grep '"version"' frontend/package.json
head -5 CHANGELOG.md
test -f docs/sprints/archive/sprint-06.md && ! test -f docs/sprints/sprint-06.md && echo "archived"
gh pr view --json state,title
```

---

**Rollback:**

```bash
git mv docs/sprints/archive/sprint-06.md docs/sprints/sprint-06.md
git checkout -- frontend/package.json CHANGELOG.md AGENTS.md
```

---

## Sprint-End Checklist

**GitHub issues:**
- [ ] Close completed issues: `gh issue close <number>`
- [ ] Check remaining open issues: `gh issue list --state=open`
- [ ] If unresolved issues remain, add to `docs/sprints/BACKLOG.md` and plan for next sprint

**Version and changelog:**
- [ ] Bump `frontend/package.json` version: `0.5.0` → `0.6.0`
- [ ] Add `## v0.6.0 — 2026-07-21` entry to `CHANGELOG.md` with `### Add`, `### Fix`, `### Update` sections
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S06-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npm run type-check && npm run lint` passes on the final branch state (unaffected by this sprint, but must not regress)
- [ ] `cd backend && dotnet build` passes on the final branch state (unaffected by this sprint, but must not regress)
- [ ] Open PR: `claude/sprint-06-sdd-docker-storage-helm` → `main` with title `IIOT-v0.6.0: sprint-06 SDD workflow docs, Compose storage, Helm chart`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-06.md` → `docs/sprints/archive/sprint-06.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to reflect no active sprint
- [ ] Update `CHANGELOG.md` (done above)

---

## Sprint Retrospective

> Filled at sprint end.

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
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-06-sdd-docker-storage-helm
   - If not: git fetch origin main && git checkout -B claude/sprint-06-sdd-docker-storage-helm origin/main
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
18. Commit: git commit -m "IIOT-S06-{TASK-ID}: <one-line summary>"

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
| **INFRA** | DevOps agent — owns Docker, Docker Compose, GitHub Actions, env vars, and (from this sprint) `helm/` |
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **ARCH** | System designer agent — owns docs, sprint files, CHANGELOG |
| **ANALYST** | Performance analyst agent — measures metrics, no code writes |
| **Acceptance criterion** | Binary, testable assertion — TRUE or FALSE |
| **Verification command** | Shell command that proves an acceptance criterion is TRUE |
| **Rollback** | Operations that return the system to its pre-task state |
| **SDD** | Spec-Driven Development — the `REQUIREMENTS.md` → sprint file → task execution → QA verify → `CHANGELOG.md` loop this repo follows; documented in `docs/SDD_WORKFLOW.md` (this sprint) |
| **Helm chart** | `helm/iiot-fleet-app/` — Kubernetes packaging of the `db`/`backend`/`frontend`/`iiot-emitter` stack, introduced this sprint |
| **volumeClaimTemplates** | StatefulSet-managed per-pod PersistentVolumeClaim mechanism used for the `db` StatefulSet's storage, as opposed to a standalone PVC |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name (now explicit, this sprint) |
