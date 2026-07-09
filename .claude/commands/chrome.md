---
name: chrome
description: Launch Chrome at a specified URL, capture console errors, and report what's visible. Use to verify a page renders correctly or to debug frontend issues.
---

# /chrome — Chrome Browser Control

## 1. Task-Specific Instructions

Open Chrome at a given URL and report on page load state, console errors/warnings, and network failures. Use this to verify a page renders correctly or to gather evidence when debugging a frontend issue — it does not modify any code.

## 2. Arguments and Placeholders

```
/chrome [url]
```

| Placeholder | Meaning | Default |
|---|---|---|
| `{url}` | Target URL to open | `http://localhost:3000` |
| `{http_status}` | HTTP status code observed on load | — |
| `{load_time}` | Milliseconds from navigation start to load complete | — |
| `{page_title}` | `<title>` of the loaded page | — |

### Common URLs

| URL | Purpose |
|-----|---------|
| `http://localhost:3000` | Main fleet dashboard |
| `http://localhost:3000/system-design` | Architecture documentation page |
| `http://localhost:8080/swagger` | Backend Swagger UI |
| `http://localhost:8080/api/vehicles` | Raw API response |

## 3. Reusable Process Steps

1. Launch Chrome at `{url}`.
2. Wait for page to fully load (DOMContentLoaded + network idle).
3. Capture:
   - Page title
   - HTTP status code
   - Console errors (red)
   - Console warnings (yellow)
   - Failed network requests
   - Any uncaught exceptions

### MCP Chrome DevTools Commands (preferred, if `mcp__chrome-devtools__` is configured)

```
mcp__chrome-devtools__navigate { url: "{url}" }
mcp__chrome-devtools__console  // capture console output
mcp__chrome-devtools__screenshot  // capture visual state
```

### Manual Launch Commands (fallback)

```bash
# Windows
start chrome {url}

# Open DevTools automatically
start chrome --auto-open-devtools-for-tabs {url}

# Headless screenshot (requires Chrome/Chromium in PATH)
chrome --headless --screenshot=screenshot.png {url}
```

## 4. Guided Examples and References

- `/chrome` — opens the default dashboard at `http://localhost:3000`.
- `/chrome http://localhost:8080/swagger` — verify the backend Swagger UI is serving correctly after an API change.
- `/chrome http://localhost:3000/system-design` — confirm the architecture doc page renders after an `AGENTS.md`/docs update.
- Pair with `/review-ux` for a fuller functional checklist rather than just console/network capture.

## 5. Explicit Output Requirements

Always report using this exact structure, even if some sections are empty (write "none" rather than omitting the heading):

```
## Chrome Report — {url} — {datetime}

### Page Load
- Status: {HTTP status}
- Load time: {ms}
- Title: {page title}

### Console Output
- Errors: {count}
  - {error message} (file:line)
- Warnings: {count}

### Network
- Failed requests: {count}
  - {url} → {status}

### Visual State
- {description or screenshot path}
```

## 6. Template-Based Naming

Screenshots, if captured, are named:

```
chrome-{page-slug}-{YYYYMMDD}-{HHmm}.png
```

where `{page-slug}` is the URL path with `/` replaced by `-` (e.g. `system-design` for `/system-design`, `root` for `/`). Save to the scratchpad directory unless told otherwise.

## 7. Error Handling and Edge Cases

| Console Error | Likely Cause | Action |
|--------------|-------------|---|
| `WebSocket connection failed` | Backend not running or wrong `NEXT_PUBLIC_API_URL` | Report as finding; do not attempt to restart services without asking |
| `CORS error` | `FRONTEND_ORIGIN` env var not set on backend | Report and point to `backend/AGENTS.md` CORS config section |
| `Failed to fetch` | Backend API down or wrong port | Cross-check with `/devops status` before concluding it's a frontend bug |
| `Cannot read properties of undefined` | Vehicle type mismatch — check `frontend/types/vehicle.ts` | Report exact stack line; do not guess the fix here |
| `Hydration mismatch` | Server/client render difference — check for `'use client'` directive | Report the component involved |
| Chrome fails to launch at all | No Chrome/Chromium on PATH, or MCP tool unavailable | Fall back to manual launch command and note the degraded capture (no console/network data) |
| Page never reaches network-idle | Long-lived SignalR connection keeps the network open | Cap the wait at a reasonable timeout (e.g. 10s) and report load state as "settled" rather than waiting indefinitely |

## 8. Documentation and Context

- This is a read-only diagnostic command; it never edits files.
- If findings point to a real bug, hand off to the `debugger` agent or the relevant engineer agent (frontend-engineer / backend-engineer) rather than fixing inline.
- Console error → cause mappings above are heuristics specific to this codebase; verify against current `frontend/AGENTS.md` before citing as root cause, since env var names may have changed.
