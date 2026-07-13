---
name: devops
description: Run Docker Compose stack, check service health, tail logs, and rebuild services for the IIoT Fleet Telemetry System.
---

# /devops — Docker Compose Operations

## 1. Task-Specific Instructions

Run Docker Compose operations for the IIoT Fleet Telemetry System: bring the stack up/down, tail logs, check health, or rebuild a single service. Destructive actions (`reset`) must be confirmed with the user before executing, per the repo's risk-handling rules.

## 2. Arguments and Placeholders

```
/devops [action] [service]
```

| Placeholder | Meaning | Allowed values |
|---|---|---|
| `{action}` | Operation to perform | `up`, `down`, `logs`, `status`, `rebuild`, `reset`, or omitted |
| `{service}` | Target service for `logs`/`rebuild` | `backend`, `frontend`, `db`, `all` |

## Default Behavior (no `{action}`)

Run a full health check:

1. Check if Docker is running
2. Check if all services are up and healthy
3. Verify backend API responds at `http://localhost:8080/api/vehicles`
4. Verify frontend responds at `http://localhost:3000`
5. Report any unhealthy services

## 3. Reusable Process Steps

```bash
# up
docker-compose up --build -d

# down
docker-compose down

# logs {service}
docker-compose logs -f {service} --tail 50

# status
docker-compose ps

# rebuild {service}
docker-compose up --build -d {service}

# reset (destructive — MUST ask for explicit user confirmation first)
docker-compose down -v
```

### Health Check Sequence

```bash
docker-compose ps
# Expected: all three services show "(healthy)" status

curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/api/vehicles
# Expected: 200

curl -s -o /dev/null -w "%{http_code}" http://localhost:8080/swagger
# Expected: 200

curl -s -o /dev/null -w "%{http_code}" http://localhost:3000
# Expected: 200
```

## 4. Guided Examples and References

- `/devops` — full health check with no changes.
- `/devops up` — build and start the entire stack in the background.
- `/devops logs backend` — tail the last 50 lines of backend logs to diagnose a startup failure.
- `/devops rebuild frontend` — rebuild only the frontend after a dependency change, without touching backend/db.
- `/devops reset` — ask the user for explicit confirmation before running; this deletes DB data.
- See `.claude/skills/devops/SKILL.md` for the underlying Docker/Compose conventions this command follows, and `docs/` for infra architecture.

## 5. Explicit Output Requirements

Report results in this structure:

```
## DevOps Report — {action} — {datetime}

### Command Executed
- {exact command run}

### Service Status
- backend: {healthy/unhealthy/not running}
- frontend: {healthy/unhealthy/not running}
- db: {healthy/unhealthy/not running}

### Health Checks
- GET /api/vehicles: {http_code}
- GET /swagger: {http_code}
- Frontend root: {http_code}

### Issues Found
- {issue or "none"}
```

## 6. Template-Based Naming

Log captures saved to disk (if requested) use:

```
devops-logs-{service}-{YYYYMMDD}-{HHmm}.log
```

Saved to the scratchpad directory unless the user specifies otherwise.

## 7. Error Handling and Edge Cases

| Issue | Fix / Handling |
|-------|-----|
| `db` unhealthy | PostgreSQL not ready — wait 30s and retry once before reporting failure |
| `backend` not starting | Check `ConnectionStrings__Fleet` env var matches `db` service credentials |
| CORS error in browser | Verify `FRONTEND_ORIGIN=http://frontend:3000` set on backend |
| `frontend` build fails | Check `NEXT_PUBLIC_API_URL` is set at build time |
| Docker daemon not running | Report immediately and stop — do not attempt any compose command |
| `{action}` is `reset` | Never execute without explicit user confirmation in this turn; explain data loss impact first |
| `{action}` not recognized | Report the invalid action and list valid ones; do not guess intent |
| `{service}` omitted for `logs`/`rebuild` | Default to `all` for `logs`; for `rebuild`, ask which service since rebuilding "all" is a much bigger action |

## 8. Documentation and Context

- Cross-reference `docker-compose.yml` at the repo root for current service names/ports before assuming the ones listed here are unchanged.
- See root `AGENTS.md` for the sprint-completion gate — infra changes made via sprint tasks still need `Status: [x]` updates and a commit per the `IIOT-S{NN}-{TASK-ID}` convention; this command itself does not create commits.
- For CI/CD pipeline changes (as opposed to local compose operations), defer to the `devops-architech` agent.
