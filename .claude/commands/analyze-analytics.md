---
name: analyze-analytics
description: Analyze IIoT Fleet Telemetry system performance — check SignalR connection status, measure API response times, report vehicle status distribution, and count alert frequencies.
---

# /analyze-analytics — Fleet Analytics Analysis

## 1. Task-Specific Instructions

Measure and report on system performance, telemetry data patterns, and alert behavior for the IIoT Fleet Telemetry System. This command is read-only — it never modifies code, config, or data. Use it to establish a baseline before/after a change, or to diagnose a reported performance/data issue.

## 2. Arguments and Placeholders

```
/analyze-analytics [section]
```

| Placeholder | Meaning | Default |
|---|---|---|
| `{section}` | One of `api`, `distribution`, `alerts`, `logs`, `swagger`, `docker`, or omitted for all | all sections |
| `{backend_url}` | Backend base URL | `http://localhost:8080` |
| `{frontend_url}` | Frontend base URL | `http://localhost:3000` |
| `{vehicle_id}` | Sample vehicle ID used for log inspection | `VEH-00000` |
| `{datetime}` | ISO-8601 timestamp at report generation time | current time |

## Prerequisites

- Backend running at `{backend_url}`
- Frontend running at `{frontend_url}`
- See `backend/AGENTS.md` and `frontend/AGENTS.md` for endpoint contracts before interpreting results

## 3. Reusable Process Steps

### Step 1 — API Performance

```bash
curl -w "HTTP: %{http_code} | Time: %{time_total}s | Size: %{size_download} bytes\n" \
  -o /dev/null -s {backend_url}/api/vehicles
# Target: HTTP 200, Time < 0.500s
```

### Step 2 — Vehicle Status Distribution

```bash
curl -s {backend_url}/api/vehicles | \
  python -c "
import json, sys
from collections import Counter
vehicles = json.load(sys.stdin)
total = len(vehicles)
dist = Counter(v['status'] for v in vehicles)
print(f'Total vehicles: {total}')
for status, count in sorted(dist.items()):
    print(f'  {status}: {count} ({count/total*100:.1f}%)')
"
```

**Expected distribution:**
- active: ~9,950+ (>99%)
- warning: ≤24
- danger: ≤14
- offline: ≤12

### Step 3 — Alert Threshold Analysis

```bash
curl -s {backend_url}/api/vehicles | \
  python -c "
import json, sys
vehicles = json.load(sys.stdin)
fuel_alerts    = [v for v in vehicles if v.get('fuel', 100) < 20]
temp_alerts    = [v for v in vehicles if v.get('temp', 0) > 65]
speed_alerts   = [v for v in vehicles if v.get('speedKph', 0) > 80]
engine_alerts  = [v for v in vehicles if v.get('engineHealth', 100) < 15]
print(f'Low fuel (<20%):       {len(fuel_alerts)} vehicles')
print(f'High temp (>65C):      {len(temp_alerts)} vehicles')
print(f'High speed (>80kph):   {len(speed_alerts)} vehicles')
print(f'Engine alert (<15):    {len(engine_alerts)} vehicles')
"
```

### Step 4 — Sample Vehicle Logs

```bash
curl -s {backend_url}/api/vehicles/{vehicle_id}/logs | \
  python -c "
import json, sys
logs = json.load(sys.stdin)
print(f'Log entries: {len(logs)}')
for log in logs[:5]:
    print(f'  [{log[\"level\"]}] {log[\"ts\"]}: {log[\"message\"]}')
"
```

### Step 5 — Swagger Endpoint Count

```bash
curl -s {backend_url}/swagger/v1/swagger.json | \
  python -c "
import json, sys
spec = json.load(sys.stdin)
paths = spec.get('paths', {})
print(f'Documented endpoints: {len(paths)}')
for path in paths:
    methods = list(paths[path].keys())
    print(f'  {\" \".join(m.upper() for m in methods)} {path}')
"
```

### Step 6 — Docker Service Health

```bash
docker-compose ps
docker stats --no-stream --format \
  "table {{.Name}}\t{{.CPUPerc}}\t{{.MemUsage}}"
```

## 4. Guided Examples and References

- `/analyze-analytics` — run all six steps and produce a full report.
- `/analyze-analytics api` — run only Step 1 (API performance), useful right after a backend change.
- `/analyze-analytics distribution` — run only Step 2, useful after touching the simulation service.
- See the ANALYST agent (`.claude/agents/analyst.md`) for the fuller version of this workflow when deeper investigation is needed.
- See `docs/` for the system architecture referenced by alert thresholds.

## 5. Explicit Output Requirements

Always produce a report in exactly this format, filling every field — never omit a section even if a step wasn't run (mark it `N/A — not requested`):

```
## Analytics Report — {datetime}

### API Performance
- GET /api/vehicles: {time}s ({http_code})
- Target: <500ms ✓/✗

### Vehicle Distribution
- Total: {count}
- active: {count} ({%})
- warning: {count} ({%}) — cap: 24
- danger: {count} ({%}) — cap: 14
- offline: {count} ({%}) — cap: 12
- Distribution within caps: ✓/✗

### Alert Thresholds
- Low fuel: {count} vehicles
- High temp: {count} vehicles
- High speed: {count} vehicles
- Engine health alerts: {count} vehicles

### Infrastructure
- Backend CPU/Memory: {values}
- Frontend CPU/Memory: {values}
- DB CPU/Memory: {values}
```

## 6. Template-Based Naming

If the report is saved to a file rather than printed inline, name it:

```
analytics-report-{YYYYMMDD}-{HHmm}.md
```

Place it under the scratchpad directory unless the user names a destination.

## 7. Error Handling and Edge Cases

| Condition | Handling |
|---|---|
| Backend not reachable | Report `HTTP: connection refused` for that step, do not fail the whole command — continue with remaining steps and note the outage in the report header |
| `curl` returns non-200 | Record the actual status code, flag with ✗, do not retry more than once |
| Empty vehicle list (`total == 0`) | Report distribution as N/A with a note that the simulation service may not be seeded — do not divide by zero |
| Vehicle distribution exceeds documented caps | Flag with ✗ and call out which cap was exceeded; do not silently round or suppress |
| `python` not on PATH | Fall back to raw `curl` output and note that structured parsing was skipped |
| Docker not running | Skip Step 6, report `Docker: not running`, continue with other steps |

## 8. Documentation and Context

- This command is read-only; it does not require the sprint-file confirmation gate in the root `AGENTS.md` since no code changes occur.
- Cross-reference thresholds and endpoint contracts against `backend/AGENTS.md` before flagging a deviation as a bug — thresholds may have been intentionally changed in a recent sprint.
- If this command reveals a genuine regression, hand off findings to the `debugger` agent rather than attempting a fix here.
