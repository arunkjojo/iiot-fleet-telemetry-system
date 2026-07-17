# Sprint Backlog / Roadmap

Tracks unresolved items and sprints planned but not yet authored as full `docs/sprints/sprint-NN.md` files.

---

## Status: 2026-07-13 Operator Brief — FULLY DELIVERED

All 9 tasks from the 2026-07-13 operator brief are now shipped. Given the sprint template's
3-12 granular-task cap, the brief was split into 3 themed sprints (operator-approved), plus one
task pulled forward and fixed standalone:

| Sprint / Branch | Brief Tasks Covered | Version | Status |
|---|---|---|---|
| Sprint 03 — `docs/sprints/archive/sprint-03.md` | 1, 3, 8 | `v0.3.0` | Shipped, merged to `main` via PR #2 |
| Standalone — `claude/fix-docker-image-ci-workflow` | 7 | — (CI fix, no app version bump) | Shipped, merged to `main` via PR #3 |
| Sprint 04 — `docs/sprints/archive/sprint-04.md` | 2, 4, 5, 6 (+ ad hoc `BE-008`, `BE-009`) | `v0.4.0` | Shipped, merged to `main` via PR #4 |
| Sprint 05 — `docs/sprints/archive/sprint-05.md` | 9 | `v0.5.0` | Shipped, merged to `main` via PR #5 |

No new sprint is currently active — `AGENTS.md`'s `## Current Sprint` points here. The next
sprint should be authored via the `sprint` skill once new scope is defined, starting from the
carryover items below.

---

## Still Open (Carryover)

These are genuinely unresolved and were never in scope for any of the three brief sprints:

1. **Frontend lint/type-check tooling gap** — `frontend/package.json` has no `lint`/`type-check`
   npm scripts and no ESLint config/dependency exists anywhere in `frontend/`, despite
   `frontend/AGENTS.md` and `REQUIREMENTS.md` NF-13/NF-14 documenting both as required
   pre-commit gates. Found during Sprint 03's UI-010/UI-011; every sprint's frontend
   verification commands assume these scripts exist and have not actually been runnable as
   written. Needs a standalone fix: add the scripts + an ESLint config.
2. **Full-scale NF-01/NF-03 load validation** — Sprint 03's ANALYST-001 ran against a
   reduced-scale local stack (`VEHICLE_COUNT=300`, not 10,000) due to sandbox constraints.
   NF-01 (10k vehicles, 60fps) and precise NF-03 (SignalR ~500ms cadence) were not validated at
   full production scale; NF-02 passed at reduced scale (p95 ≈ 109ms). A full-scale load test
   pass is recommended before relying on these numbers at 10,000 vehicles.
3. **`ILiveTelemetryStore` cold-start hydration gap** — found during Sprint 04's `BE-009`:
   `ILiveTelemetryStore` is never hydrated from Postgres's DB-seeded `display_number`
   (`FL-NNNNN`) on backend startup, so a freshly-started live-mode backend shows
   `displayNumber: ""` for every vehicle until an operator PATCHes one in. Intentionally left
   out of `BE-009`'s scope (that task fixed a data-loss/clobbering bug, not this cold-start gap).
   Needs a standalone fix: populate `ILiveTelemetryStore` from the `vehicles` table on backend
   startup (or on first ingest per vehicle) before `USE_LIVE_TELEMETRY=true` traffic begins.
4. **Local .NET runtime/SDK version mismatch** — found during Sprint 07's `QA-007`: this dev
   machine's installed shared runtimes top out at `8.0.23`, but the backend's built binary
   requests `8.0.28`, so `dotnet run` fails with "You must install or update .NET to run this
   application" even though `dotnet build` succeeds cleanly. Blocked QA-007's live-mode runtime
   smoke test (sampling `GET /api/vehicles` to confirm the new status-distribution ranges).
   Needs either an installed 8.0.28 (or newer 8.0.x) shared runtime, or a `global.json`/SDK pin
   compatible with what's actually installed.

---

## Sprint Detail (historical reference)

### Sprint 04 (shipped, v0.4.0) — UX & Search

**Theme:** Editable vehicle/driver metadata, general UI polish, search, and the 10-vehicle focused view.

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 2 | Editable vehicle number / driver name fields in the UI | Operator confirmed: add UI to rename a vehicle / reassign driver name from the dashboard, with validation, persisted to the `vehicles` table. Needs a new backend `PATCH`/`PUT` endpoint + EF Core update + frontend form. Not previously possible — no such endpoint exists today. |
| Task 4 | UI modifications — usability/aesthetics, avoid layout overflow ("screen side point issues"), responsive across screen sizes | Needs a screenshot/example from the operator of the specific layout issue before scoping precisely — currently underspecified beyond "avoid overflow, stay responsive." |
| Task 5 | Search by vehicle number/driver name + date-time filter (last 24h) | Operator confirmed: search should exclude vehicles with no `telemetry_snapshots` activity in the last 24 hours, in addition to today's text search. |
| Task 6 | Default "focused view" — sidebar shows max 10 curated vehicles by default, inactive vehicles hidden, with a "show all 10,000" toggle | Depends on Sprint 03's `inactive` flag (`UI-011`) and `hideInactive` toggle already existing in `useFilterStore.ts` — Sprint 04 adds the *default-on* curated view on top of that mechanism. Does not violate NF-01 (10k render) since the full list remains one toggle away. |

**Status:** Shipped in `v0.4.0` (`docs/sprints/archive/sprint-04.md`). All 12 tasks `[x]` — the 11
originally planned plus a bonus ad hoc fix, `BE-009`, found by `QA-003`'s first verification pass:
`PATCH /api/vehicles/{id}` edits were being silently clobbered by the next live-ingestion tick
(`TelemetryIngestController` rebuilt a fresh `Vehicle` object per tick without preserving an
edited `DriverName`/`DisplayNumber`). Fixed by making the live store's existing state win over
the incoming ingest request for those two fields; re-verified holding across multiple ingest
ticks. See the `ILiveTelemetryStore` cold-start hydration gap in "Still Open (Carryover)" above
for the related follow-up this fix surfaced.

---

### Sprint 05 (shipped, v0.5.0) — Project Documentation

**Theme:** Comprehensive project documentation (`docs/sprints/archive/sprint-05.md`).

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 7 | ~~GitHub Actions Docker build failing~~ — **DONE**, shipped standalone on branch `claude/fix-docker-image-ci-workflow` (PR #3, merged to `main`). `.github/workflows/docker-image.yml` now builds all 4 real service images (`db`, `backend`, `frontend`, `iiot-emitter`) via a matrix strategy instead of a nonexistent root `Dockerfile`. | Pulled forward, not part of Sprint 05's task list. |
| Task 9 | Comprehensive documentation: architecture, DevOps practices, AI-assisted workflow (Claude Code agents/skills), use case, onboarding | Delivered as `ARCH-009`: new `docs/PROJECT_OVERVIEW.md` (7 sections), linked from `README.MD`. Verified link-integrity and factual accuracy by `QA-005`. |

**Status:** Shipped in `v0.5.0` (`docs/sprints/archive/sprint-05.md`, PR #5). All 3 tasks `[x]`
(`ARCH-009`, `QA-005`, `ARCH-010`).

---

## Notes

- Sprint 03 (`docs/sprints/archive/sprint-03.md`, merged to `main` via PR #2) covers Tasks 1, 3, 8 from the same brief (SignalR connection-status visibility, client-side inactive-vehicle detection, telemetry retention policy).
- Task 7 (the CI fix) shipped as a standalone out-of-band fix, branch `claude/fix-docker-image-ci-workflow` (PR #3) — merged to `main`.
- Sprint 04 (`docs/sprints/archive/sprint-04.md`, PR #4) covers Tasks 2, 4, 5, 6, plus a bonus fix found while scoping Task 2: `TelemetrySimulationService.MakeId()` generates random gibberish IDs in dummy mode instead of the `VEH-NNNNN` format used everywhere else — fixed as `BE-008`.
- For the still-unresolved items (lint tooling, full-scale load test, cold-start hydration gap), see "Still Open (Carryover)" above — kept in one place rather than duplicated here.
