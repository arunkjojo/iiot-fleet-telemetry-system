# Sprint Backlog / Roadmap

Tracks unresolved items and sprints planned but not yet authored as full `docs/sprints/sprint-NN.md` files.

---

## Origin

2026-07-13 operator brief listed 9 tasks. Given the sprint template's 3-12 granular-task cap, the brief was split into 3 themed sprints (operator-approved). Sprint 03 shipped (merged to `main` via PR #2). Sprint 04 (`docs/sprints/sprint-04.md`) and Sprint 05 (`docs/sprints/sprint-05.md`) are now both authored in full.

---

## Sprint 04 (active) — UX & Search

**Theme:** Editable vehicle/driver metadata, general UI polish, search, and the 10-vehicle focused view.

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 2 | Editable vehicle number / driver name fields in the UI | Operator confirmed: add UI to rename a vehicle / reassign driver name from the dashboard, with validation, persisted to the `vehicles` table. Needs a new backend `PATCH`/`PUT` endpoint + EF Core update + frontend form. Not previously possible — no such endpoint exists today. |
| Task 4 | UI modifications — usability/aesthetics, avoid layout overflow ("screen side point issues"), responsive across screen sizes | Needs a screenshot/example from the operator of the specific layout issue before scoping precisely — currently underspecified beyond "avoid overflow, stay responsive." |
| Task 5 | Search by vehicle number/driver name + date-time filter (last 24h) | Operator confirmed: search should exclude vehicles with no `telemetry_snapshots` activity in the last 24 hours, in addition to today's text search. |
| Task 6 | Default "focused view" — sidebar shows max 10 curated vehicles by default, inactive vehicles hidden, with a "show all 10,000" toggle | Depends on Sprint 03's `inactive` flag (`UI-011`) and `hideInactive` toggle already existing in `useFilterStore.ts` — Sprint 04 adds the *default-on* curated view on top of that mechanism. Does not violate NF-01 (10k render) since the full list remains one toggle away. |

---

## Sprint 05 (authored) — Project Documentation

**Theme:** Comprehensive project documentation (`docs/sprints/sprint-05.md`).

**Source tasks from the 2026-07-13 brief:**

| Brief Task | Summary | Notes |
|-----------|---------|-------|
| Task 7 | ~~GitHub Actions Docker build failing~~ — **DONE**, shipped standalone on branch `claude/fix-docker-image-ci-workflow` (not yet merged to `main` as of Sprint 04's authoring) — see Notes below. | No longer part of Sprint 05's scope. |
| Task 9 | Comprehensive documentation: architecture, DevOps practices, AI-assisted workflow (Claude Code agents/skills), use case, onboarding | Scoped in full in `docs/sprints/sprint-05.md` (`ARCH-009`): new `docs/PROJECT_OVERVIEW.md`, linked from `README.md`. |

---

## Notes

- Sprint 03 (`docs/sprints/archive/sprint-03.md`, merged to `main` via PR #2) covers Tasks 1, 3, 8 from the same brief (SignalR connection-status visibility, client-side inactive-vehicle detection, telemetry retention policy).
- Task 7 (the CI fix) shipped standalone on `claude/fix-docker-image-ci-workflow` (2 commits: the workflow fix + a `BACKLOG.md` note) — not yet merged to `main`. Merge/PR that branch independently of the Sprint 04/05 branches.
- Sprint 04 (`docs/sprints/sprint-04.md`) covers Tasks 2, 4, 5, 6, plus a bonus fix found while scoping Task 2: `TelemetrySimulationService.MakeId()` generates random gibberish IDs in dummy mode instead of the `VEH-NNNNN` format used everywhere else — fixed as `BE-008`.
- `frontend/package.json` has no `lint`/`type-check` npm scripts and no ESLint config/dependency exists anywhere in `frontend/`, despite `frontend/AGENTS.md` and `REQUIREMENTS.md` NF-13/NF-14 documenting both as required pre-commit gates (found during Sprint 03's UI-010/UI-011). Every sprint's frontend verification commands assume these scripts exist and have not actually been runnable as written — needs a standalone fix (add the scripts + an ESLint config) before this gate can be enforced for real.
- Sprint 03's ANALYST-001 ran against a reduced-scale local stack (`VEHICLE_COUNT=300`, not 10,000) due to sandbox constraints — NF-01 (10k vehicles, 60fps) and precise NF-03 (SignalR ~500ms cadence) were not validated at full production scale. A full-scale load test pass is recommended before relying on this sprint's NF-02 latency numbers at 10,000 vehicles.
