---
name: review-ux
description: Open Chrome and verify the IIoT Fleet Telemetry dashboard renders correctly, displays live vehicle data, and has no console errors. Use after frontend changes or to confirm the full stack is working end-to-end.
---

# /review-ux — UX Review

## 1. Task-Specific Instructions

Open the fleet telemetry dashboard in Chrome and perform a visual and functional review against the checklist below. Use after frontend changes, or to confirm the full stack works end-to-end. This command is read-only — it verifies, it does not fix.

## 2. Arguments and Placeholders

```
/review-ux [section]
```

| Placeholder | Meaning | Default |
|---|---|---|
| `{section}` | One of `load`, `list`, `realtime`, `detail`, `alerts`, `filters`, `console`, or omitted for all | all sections |
| `{frontend_url}` | Frontend base URL | `http://localhost:3000` |
| `{backend_url}` | Backend base URL | `http://localhost:8080` |

## Prerequisites

- Full stack running (`docker-compose up` or local dev servers) — verify with `/devops status` first if unsure
- Backend at `{backend_url}`
- Frontend at `{frontend_url}`

## 3. Reusable Process Steps (Review Checklist)

### Step 1 — Initial Load

- [ ] Dashboard loads at `{frontend_url}` without errors
- [ ] Header renders with "IIOT Fleet Telemetry" title and notification bell
- [ ] Sidebar appears on the left with vehicle list
- [ ] Map appears in the center
- [ ] Detail panel placeholder visible on the right

### Step 2 — Vehicle List (Sidebar)

- [ ] Vehicle list shows vehicles (should be ~10,000 entries)
- [ ] Status filter buttons visible: All, Active, Warning, Danger, Offline
- [ ] Vehicle counts shown next to each filter
- [ ] Scrolling the list is smooth (no jank — virtualization working)
- [ ] Search input responds within 200ms
- [ ] Searching by "VEH-000" narrows the list correctly

### Step 3 — Real-Time Updates

- [ ] Vehicle positions update on the map every ~1 second
- [ ] Vehicle status colors update in real-time
- [ ] Fuel/speed/temp values change over time in the sidebar

### Step 4 — Vehicle Detail Panel

- [ ] Clicking a vehicle opens the detail panel
- [ ] All 5 gauges render: Fuel, Temperature, Speed, Cargo, Engine Health
- [ ] Vehicle ID, driver name, model, and status badge display correctly
- [ ] Logs section loads (may be empty if no events yet)

### Step 5 — Alert System

- [ ] Wait 30 seconds — at least one toast notification should appear
- [ ] Toast auto-dismisses after 2 seconds
- [ ] Clicking the notification bell opens the notification modal
- [ ] Notifications show vehicle ID, metric, and timestamp

### Step 6 — Status Filters

- [ ] Clicking "Danger" filter shows only danger vehicles on map and sidebar
- [ ] Clicking "All" restores full list
- [ ] "Offline" filter shows vehicles with offline status

### Step 7 — Console Check

- [ ] Open DevTools (F12) → Console
- [ ] No red errors
- [ ] No failed network requests
- [ ] SignalR connection shows as connected (no WebSocket errors)

### Automated Commands

```bash
# Open dashboard in Chrome
start chrome {frontend_url}

# Check for JS errors via MCP Chrome DevTools (if configured)
# Use mcp__chrome-devtools__ tools to capture console output
```

## 4. Guided Examples and References

- `/review-ux` — run the full 7-step checklist end-to-end.
- `/review-ux realtime` — run only Step 3, useful after a SignalR hub change.
- `/review-ux console` — run only Step 7, a quick smoke check after any frontend deploy.
- Use `/chrome {frontend_url}` first if you only need raw console/network capture without the full checklist.
- See `frontend/AGENTS.md` for component-level conventions (gauges, sidebar virtualization, SignalR client) referenced by this checklist.

## 5. Explicit Output Requirements

After reviewing, report in this exact structure:

```
## UX Review Report — {datetime}

### Checklist Results
- {step name}: ✓/✗ — {note if ✗}
... (one line per checklist item actually run)

### Screenshots
- {path or "none captured"}

### Console Errors
- {exact error text} (file:line) — or "none"

### Performance
- Frame rate: {value or "not measured"}
- Load time: {value}

### Recommended Fix
- {description} → route to {NEXT | ASP.NET | INFRA}
```

## 6. Template-Based Naming

Screenshots of failing checks are named:

```
review-ux-{step-slug}-{YYYYMMDD}-{HHmm}.png
```

where `{step-slug}` is the failing checklist item in kebab-case (e.g. `sidebar-scroll-jank`). Save to the scratchpad directory unless told otherwise.

## 7. Error Handling and Edge Cases

| Condition | Handling |
|---|---|
| Frontend not reachable at all | Stop immediately, report as a blocking failure, suggest running `/devops` first — do not mark checklist items as failed if the page never loaded |
| No toast appears within 30s (Step 5) | Wait once more up to 60s total before marking ✗ — alert timing has natural jitter |
| Vehicle count far below ~10,000 | Flag as a data-seeding issue, not a UI bug; cross-check with `/analyze-analytics distribution` |
| Virtualized list appears janky | Note approximate frame rate if observable; do not guess a root cause — hand off to `frontend-engineer` or `debugger` |
| SignalR shows disconnected | Check this isn't due to backend being down first (`/devops status`) before treating as a frontend bug |
| MCP Chrome DevTools unavailable | Fall back to manual Chrome + F12 inspection and note the review was done without automated console capture |

## 8. Documentation and Context

- This command is read-only and verification-only; any fix it uncovers should be handed to the appropriate agent (frontend-engineer, backend-engineer, devops-architech) or reported to the user, not patched here.
- Cross-check checklist expectations (e.g. ~10,000 vehicles, gauge set, alert cadence) against the current sprint file referenced in root `AGENTS.md` before treating a deviation as a regression — these numbers may change between sprints.
- Pairs well with the `quality-analyst` agent for a more exhaustive acceptance-criteria pass.
