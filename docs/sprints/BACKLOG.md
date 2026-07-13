# Sprint Backlog / Roadmap

Tracks unresolved items and sprints planned but not yet authored as full `docs/sprints/sprint-NN.md` files.

---

## Origin

2026-07-13 operator brief listed 9 tasks. Given the sprint template's 3-12 granular-task cap, the brief was split into 3 themed sprints (operator-approved). Sprint 03 is authored in full (`docs/sprints/sprint-03.md`); Sprint 04 and Sprint 05 are scoped below and will be authored via the `sprint` skill when Sprint 03 closes.

---

## Sprint 04 (planned) — UX & Search

**Theme:** Editable vehicle/driver metadata, general UI polish, search, and the 10-vehicle focused view.

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 2 | Editable vehicle number / driver name fields in the UI | Operator confirmed: add UI to rename a vehicle / reassign driver name from the dashboard, with validation, persisted to the `vehicles` table. Needs a new backend `PATCH`/`PUT` endpoint + EF Core update + frontend form. Not previously possible — no such endpoint exists today. |
| Task 4 | UI modifications — usability/aesthetics, avoid layout overflow ("screen side point issues"), responsive across screen sizes | Needs a screenshot/example from the operator of the specific layout issue before scoping precisely — currently underspecified beyond "avoid overflow, stay responsive." |
| Task 5 | Search by vehicle number/driver name + date-time filter (last 24h) | Operator confirmed: search should exclude vehicles with no `telemetry_snapshots` activity in the last 24 hours, in addition to today's text search. |
| Task 6 | Default "focused view" — sidebar shows max 10 curated vehicles by default, inactive vehicles hidden, with a "show all 10,000" toggle | Depends on Sprint 03's `inactive` flag (`UI-011`) and `hideInactive` toggle already existing in `useFilterStore.ts` — Sprint 04 adds the *default-on* curated view on top of that mechanism. Does not violate NF-01 (10k render) since the full list remains one toggle away. |

---

## Sprint 05 (planned) — Infra & Documentation

**Theme:** CI fix and comprehensive project documentation.

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 7 | GitHub Actions Docker build failing: `.github/workflows/docker-image.yml:18` runs `docker build . --file Dockerfile` at the repo root, but no `Dockerfile` exists there — the 4 real Dockerfiles live under `backend/`, `frontend/`, `db/`, `iiot-emitter/`. Root cause already identified during Sprint 03 planning. | Fix: replace the single build step with a matrix strategy building all 4 service images from their actual paths. Small, low-risk, could be pulled forward and fixed standalone before Sprint 05 if the operator wants CI green sooner. |
| Task 9 | Comprehensive documentation: architecture, DevOps practices, AI-assisted workflow (Claude Code agents/skills), use case, onboarding | Operator confirmed: new `docs/PROJECT_OVERVIEW.md`, linked from `README.md`. |

---

## Notes

- Sprint 03 (`docs/sprints/sprint-03.md`) covers Tasks 1, 3, 8 from the same brief (SignalR connection-status visibility, client-side inactive-vehicle detection, telemetry retention policy).
- If the operator wants Task 7 (the CI fix) fixed immediately rather than waiting for Sprint 05, it is small enough to run as a standalone out-of-band fix — flag this to the user before Sprint 05 is authored.
