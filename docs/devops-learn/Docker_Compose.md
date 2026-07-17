# Learning Guide: Docker & Docker Compose

> Concepts-first guide. For step-by-step run commands for this repo, see [`DOCKER_README.md`](../DOCKER_README.md).

---

## 1. What is Docker

Docker packages an application together with everything it needs to run — runtime, libraries, config — into a single unit called an **image**. Running that image produces a **container**: an isolated process with its own filesystem, network interface, and process tree, but sharing the host OS kernel (unlike a virtual machine, which virtualizes an entire OS).

Key ideas:

- **Image** — a read-only, layered filesystem snapshot built from a `Dockerfile`. Each instruction (`FROM`, `RUN`, `COPY`, …) adds a layer; layers are cached and shared across images, which is why rebuilds after a small source change are fast.
- **Container** — a running instance of an image, with a thin writable layer on top. Ephemeral by default: stop it, and any writes to that layer are gone (which is why persistent data needs a **volume**, see below).
- **Containers vs. VMs** — a VM virtualizes hardware and boots a full guest OS (minutes, GBs). A container shares the host kernel and starts in milliseconds, at megabytes. This is why you can run three services (db/backend/frontend) as containers on a laptop without the overhead of three VMs.

## 2. What is Docker Compose

A single `docker run` command configures one container. Real applications are usually *several* containers that need to talk to each other (a database, an API, a UI) — Compose is the tool for declaring that whole stack in one YAML file (`docker-compose.yml`) and bringing it up/down as a unit with one command.

Compose gives you, declaratively, what you'd otherwise script by hand:
- A shared **network** so containers can reach each other by service name (no manual IP wiring)
- **Startup ordering** — `depends_on` (optionally gated on a `healthcheck`, not just "container started")
- **Volumes** for data that must outlive a container restart
- Per-service **environment variables**, ports, and restart policies, all in one reviewable file

## 3. Why we use them

- **Parity** — the same `docker-compose.yml` that a developer runs locally is structurally close to what CI and (via the Helm chart — see [`Helm.md`](Helm.md)) production run. Fewer "works on my machine" surprises.
- **Health-gated startup** — `depends_on: condition: service_healthy` means the backend doesn't attempt to reach Postgres before Postgres is actually accepting connections, and the emitter doesn't POST telemetry before the backend is listening. Without this, the first N seconds of any fresh `docker-compose up` would be a crash-loop.
- **Network isolation** — services on a Compose network can reach each other by name, but nothing outside the network can reach them except through explicitly published ports. This is a meaningful default-secure posture for a multi-service app.

## 4. How to use — general

```bash
docker build -t myimage:tag .        # build one image from a Dockerfile
docker run -p 8080:8080 myimage:tag  # run one container from it

docker compose up --build            # build + start every service in docker-compose.yml
docker compose up -d                 # same, detached (background)
docker compose ps                    # service status + health
docker compose logs -f <service>     # follow one service's logs
docker compose down                  # stop and remove containers (volumes survive)
docker compose down -v               # also remove volumes (destructive — wipes data)
```

`docker-compose.yml` anatomy, the parts that matter most:

| Key | Purpose |
|-----|---------|
| `services.<name>.build` | Where the Dockerfile for that service lives |
| `services.<name>.environment` | Env vars injected into the container at start |
| `services.<name>.depends_on` | Startup ordering, optionally `condition: service_healthy` |
| `services.<name>.healthcheck` | The command Compose runs to decide "healthy" |
| `volumes` | Named volumes for data that must survive container restarts |
| `networks` | Custom networks; services on the same network resolve each other by service name |

## 5. How this project uses them

This repo's `containers/docker-compose.yml` defines four services on one custom network, `iiot-fleet-net`:

```
db (stock postgres:16-alpine — no build, pulled directly)
  └─ healthcheck: pg_isready

backend (ASP.NET Core 8, built from containers/backend/Dockerfile, context ../backend)
  └─ depends_on: db (service_healthy)
  └─ healthcheck: /dev/tcp probe on :8080 (the aspnet base image has neither curl nor wget)

frontend (Next.js 15, built from containers/frontend/Dockerfile, context ../frontend)
  └─ depends_on: backend (started — not health-gated in this service's depends_on entry)
  └─ healthcheck: wget spider on :3000 (node:18-alpine has wget, not curl)

emitter (Python telemetry simulator, built from containers/emitter/Dockerfile, context ../emitter)
  └─ depends_on: backend (service_healthy)
```

Notable specifics:

- **`ConnectionStrings__Fleet`** on `backend` is ASP.NET Core's double-underscore convention for nested config — it binds to `ConnectionStrings:Fleet` inside the app. See `.claude/skills/devops/SKILL.md` for the full environment-variable rule set this project follows.
- **`postgres_data`** is the one named volume — without it, `docker compose down` (even without `-v`) would still preserve data since volumes aren't removed by a plain `down`, but a full teardown/rebuild of the `db` container would otherwise lose all rows.
- **Multi-stage Dockerfiles** — both `containers/backend/Dockerfile` and `containers/frontend/Dockerfile` build in one stage (SDK / full `node`) and copy only the compiled output into a slim runtime stage (`aspnet` / `node:alpine`). This keeps the shipped image small and avoids bundling build-only tooling (the .NET SDK, dev dependencies) into what actually runs.
- **`emitter`** only starts once `backend` reports healthy — it POSTs synthetic telemetry to `http://backend:8080`, and it has nothing useful to do before the API is up.
- **Dockerfiles live under `containers/<service>/Dockerfile`**, separate from each service's source directory. Each service's `build.context` still points at its real source dir (`../backend`, `../frontend`, `../emitter`) so the Dockerfile's `COPY` instructions keep working unchanged — only where the Dockerfile *file* lives moved, not what it builds from.

Run commands, troubleshooting, and env var setup for actually standing this stack up locally: [`DOCKER_README.md`](../DOCKER_README.md).

**See also:** [`Helm.md`](Helm.md) · [`K8s.md`](K8s.md)
