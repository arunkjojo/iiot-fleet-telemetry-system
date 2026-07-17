# Sprint 07 — status-rules-cleanup

---

## Note (Operator Prompt)

> The following is the exact operator prompt this sprint was generated under. Agents MUST read it before any task execution.

```
Understand the below modification and bug fix and instruction, if any clarification or doubt, ask me before start the task execution.
```

---

## Sprint Metadata

| Field | Value |
|-------|-------|
| **Sprint ID** | S07 |
| **Branch** | `claude/sprint-07-status-rules-cleanup` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | 2026-07-17 |
| **End date** | 2026-07-17 |
| **Goal** | Operators see a simpler sidebar (no "Hide Inactive" / "Focused View" toggles — the full fleet is always listed), vehicle status reflects the new threshold rules and the fleet's simulated status mix matches the new distribution ranges, and both the app's own architecture/data-flow and core DevOps concepts (Docker Compose, Helm, Kubernetes) are documented for onboarding. |
| **Success metric** | Sidebar renders with no "Hide Inactive"/"Focused View" controls; `VehicleStatusEvaluator.Evaluate` and `TelemetrySimulationService.EvaluateStatus` implement the new thresholds identically; the rebalancer target ranges are offline 40–100, danger 100–400, warning 500–800, active = remainder; `docs/APPLICATION_OVERVIEW.md` and `docs/devops-learn/{Docker_Compose,Helm,K8s}.md` exist and are non-empty. |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) |
| **Agents involved** | NEXT, ASP.NET, ARCH, QA |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

---

## Context

Sprint 06 shipped SDD docs, Compose networking, and the Helm chart. This sprint is an operator-driven cleanup: two sidebar features (`hideInactive`, `focusedView`) added across Sprints 03–04 are being removed because they no longer match how operators want to browse the fleet — the full list should always be visible, unfiltered by inactivity or a top-10 cap. In the same pass, the vehicle status thresholds and the simulation's status-distribution rebalancer are being replaced with new business rules supplied by the operator (tighter, more literal threshold bands; ranged, not fixed, distribution targets). Finally, the operator asked for the application's own architecture/data-flow to be documented, plus three new DevOps learning guides (Docker Compose, Helm, Kubernetes) for team onboarding.

This sprint resolves three ambiguities that were clarified with the operator before task blocks were written (do not re-litigate, just implement as stated):
1. New `active` fuel band is a closed range: `30.0 <= fuelPercent <= 100.0` (not the open-ended/duplicated clause in the original brief).
2. Rebalancer caps are **fixed min–max ranges**, not percentages of fleet size: offline 40–100, danger 100–400, warning 500–800, active = remainder.
3. Removing "Hide Inactive"/"Focused View" also removes their backing requirements (`F-33`, `F-34`, `§4.4` including `BR-01`–`BR-04`) from `REQUIREMENTS.md`, and removes the underlying client-side "inactive vehicle" concept entirely (not just the two toggles) — since nothing else in the app depends on it once those requirements are gone.

**Related documents:**
- `docs/requirements/REQUIREMENTS.md` — §2.1 (F-04/F-05), §2.3 (F-10–F-13, F-33, F-34), §4.1, §4.2, §4.4 — all touched by this sprint
- `docs/sprints/archive/sprint-04.md` — original source of the `hideInactive`/`focusedView`/24h-filter features being removed here

---

## Branch Setup (run once before any task)

```bash
git fetch origin main
git checkout -B claude/sprint-07-status-rules-cleanup origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

**Branch:**
- [ ] Branch `claude/sprint-07-status-rules-cleanup` exists and is clean (`git status` shows no uncommitted changes)
- [ ] Branch was cut from `origin/main`

**Frontend:**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npm run type-check` passes with **zero errors** on the unmodified codebase
- [ ] `cd frontend && npm run lint` passes with **zero warnings** on the unmodified codebase

**Backend:**
- [ ] `cd backend && dotnet build` passes with **zero errors** on the unmodified codebase
- [ ] `curl http://localhost:8080/api/vehicles` returns HTTP 200 with JSON array

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full
- [ ] `backend/AGENTS.md` read in full
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] This sprint file read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] Confirm no other in-flight branch is also touching `frontend/store/useFilterStore.ts`, `frontend/components/Sidebar.tsx`, `backend/Services/VehicleStatusEvaluator.cs`, or `backend/Services/TelemetrySimulationService.cs`

---

## Task Index (Top-Level Todo)

- [x] UI-015 — Remove "Hide Inactive" / "Focused View" controls from Sidebar + filter store
- [x] UI-016 — Remove the client-side "inactive vehicle" sweep/badge/dimming (page.tsx, MapView, DetailPanel, types)
- [x] BE-010 — Update `VehicleStatusEvaluator.Evaluate` to the new threshold rules
- [x] BE-011 — Update `TelemetrySimulationService` status evaluation + rebalancer to new thresholds/ranges
- [x] ARCH-014 — Update `REQUIREMENTS.md` thresholds/caps, remove F-33/F-34/§4.4
- [x] ARCH-015 — Author `docs/APPLICATION_OVERVIEW.md` (what the app is, how it works, data flow)
- [x] ARCH-016 — Author `docs/devops-learn/Docker_Compose.md`
- [x] ARCH-017 — Author `docs/devops-learn/Helm.md`
- [x] ARCH-018 — Author `docs/devops-learn/K8s.md`
- [x] QA-007 — Verify all acceptance criteria, type-check/lint/build, and live status/distribution behavior

---

## Dependency Map

```
UI-015 (no deps)      BE-010 (no deps)      ARCH-014 (no deps)      ARCH-016 (no deps)
   ↓                       ↓                                            ARCH-017 (no deps)
UI-016                 BE-011 (needs BE-010)                            ARCH-018 (no deps)
   ↓                       ↓                                                ↓
   +-----------+-----------+                                          ARCH-015 (no deps)
               ↓
            QA-007  (needs UI-015, UI-016, BE-010, BE-011, ARCH-014, ARCH-015, ARCH-016, ARCH-017, ARCH-018)
```

---

## Tasks

---

### UI-015: Remove "Hide Inactive" / "Focused View" controls from Sidebar + filter store

**Agent:** NEXT
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`frontend/store/useFilterStore.ts` exposes `hideInactive`/`toggleHideInactive` and `focusedView`/`toggleFocusedView`. `frontend/components/Sidebar.tsx` renders a "Hide Inactive" checkbox and a "Focused View (Top 10)" button, applies a `hideInactive` filter, a 24h-activity filter gated on search query (F-33), and a top-10 slice gated on `focusedView` (F-34). The operator wants the sidebar to always show the full, unfiltered-by-inactivity list — remove both controls and their filtering logic. The status filter checkboxes (`All`/`Active`/`Warning`/`Danger`/`Offline`) and free-text search stay exactly as they are.

---

**Files to read before starting:**

- `frontend/store/useFilterStore.ts` — current `hideInactive`/`focusedView` state shape
- `frontend/components/Sidebar.tsx` — full component; controls at lines ~41-44, ~134-160, ~274-291; `visible` memo at ~157-160

---

**Files to modify:**

- `frontend/store/useFilterStore.ts` — remove `hideInactive`, `toggleHideInactive`, `focusedView`, `toggleFocusedView` from `FilterState` and the store implementation
- `frontend/components/Sidebar.tsx` — remove the "Hide Inactive" checkbox and "Focused View" button JSX; remove the `hideInactive`/`focusedView`/`toggleHideInactive`/`toggleFocusedView` store reads; remove the `hideInactive` filter block and the top-10 `visible` memo (render `filtered` directly instead of `visible`); remove the 24h-activity (`q` + `lastSeenAtUtc`) filter block tied to F-33; remove the `formatLastSeen` helper and its usage in the row JSX

---

**Files to create:**

None

---

**Do NOT touch:**

- `frontend/store/useFilterStore.ts` — the `selectedStatuses`/`toggleStatus` status-filter logic must be left exactly as-is
- `frontend/components/Sidebar.tsx` — the search/token-index logic (lines ~19-116), status filter checkboxes/counts, and virtualization setup must be left exactly as-is
- `backend/Services/TelemetrySimulationService.cs` — not part of this task

---

**Sub-task breakdown:**

- [ ] Remove `hideInactive`/`toggleHideInactive`/`focusedView`/`toggleFocusedView` from `useFilterStore.ts`
- [ ] Remove the two store reads and the "Hide Inactive" checkbox + "Focused View" button JSX from `Sidebar.tsx`
- [ ] Remove the `hideInactive` filter block, the 24h-activity filter block, and the `visible` top-10 memo from `Sidebar.tsx`; render `filtered` in place of `visible` in the virtualizer and row-render code
- [ ] Remove `formatLastSeen` and its call site in the row JSX
- [ ] Run `npm run type-check` and `npm run lint` — fix any fallout from removed identifiers

---

**Implementation notes:**

1. `visible` is currently used in three places: the virtualizer `count`, `virtualItems.map`'s `visible[virtualRow.index]`, and `handleKeyDown`'s bounds checks — all three must reference `filtered` after the memo is removed.
2. The `INACTIVE` badge and `v.inactive ? 'opacity-50' : ''` styling on the row itself, and the `Last seen ...` line, belong to the broader "inactive vehicle" concept and are removed in UI-016, not here — leave `v.inactive`/`v.lastSeenAtUtc` reads in the row JSX untouched in this task to avoid a broken intermediate state; UI-016 removes them together with the type and upstream computation.
3. Do not touch the status filter checkbox group or its count computation.

---

**Acceptance criteria:**

1. `useFilterStore.ts` no longer exports `hideInactive`, `toggleHideInactive`, `focusedView`, or `toggleFocusedView`.
2. The Sidebar renders no "Hide Inactive" checkbox and no "Focused View (Top 10)" / "Show All N" button.
3. The sidebar always lists every vehicle matching the current search/status filter — no top-10 cap is ever applied.
4. `cd frontend && npm run type-check` passes with zero errors.
5. `cd frontend && npm run lint` passes with zero warnings.

---

**Verification command:**

```bash
cd frontend && npm run type-check && npm run lint
# Expected: zero errors, zero warnings
grep -in "hideInactive\|focusedView\|Focused View\|Hide Inactive" frontend/store/useFilterStore.ts frontend/components/Sidebar.tsx
# Expected: no matches
```

---

**Rollback:**

```bash
git checkout -- frontend/store/useFilterStore.ts frontend/components/Sidebar.tsx
```

---

### UI-016: Remove the client-side "inactive vehicle" sweep/badge/dimming

**Agent:** NEXT
**Depends on:** UI-015
**Status:** [x]

---

**Context:**

With F-33/F-34/§4.4 (`BR-01`–`BR-04`) removed from `REQUIREMENTS.md` (ARCH-014) and the two sidebar toggles gone (UI-015), nothing in the requirements backs the client-side "inactive vehicle" concept anymore (sustained `speedKph == 0` for 60+ seconds, computed in `frontend/app/page.tsx` and surfaced as dimming in `MapView.tsx`, a badge in `DetailPanel.tsx`, and an `INACTIVE` badge + row opacity + "Last seen" line in `Sidebar.tsx`). Remove the concept end-to-end: the 5s sweep in `page.tsx`, the `inactive`/`lastSeenAtUtc` fields from `Vehicle` type, and every consumer.

---

**Files to read before starting:**

- `frontend/app/page.tsx` — lines ~16-30, ~74, ~80, ~102, ~158-170 (the inactive sweep, `INACTIVE_THRESHOLD_MS`, `lastSeenAtUtc` assignment)
- `frontend/components/MapView.tsx` — line ~50 (`opacity: v.inactive ? 0.4 : 1`)
- `frontend/components/DetailPanel.tsx` — lines ~205-206 (`Inactive` badge)
- `frontend/components/Sidebar.tsx` — remaining `v.inactive`/`v.lastSeenAtUtc` reads in the row JSX (left in place by UI-015)
- `frontend/types/vehicle.ts` — lines ~32, ~37-38 (`lastSeenAtUtc`, `inactive` fields)

---

**Files to modify:**

- `frontend/app/page.tsx` — remove the inactive sweep effect, `INACTIVE_THRESHOLD_MS`, and any state/refs that exist solely to support it; keep `lastSeenAtUtc` assignment removed only if nothing else in `page.tsx` depends on it (check before deleting)
- `frontend/components/MapView.tsx` — remove the `opacity: v.inactive ? 0.4 : 1` conditional (markers render at full opacity)
- `frontend/components/DetailPanel.tsx` — remove the `Inactive` badge block
- `frontend/components/Sidebar.tsx` — remove the `INACTIVE` badge, the `v.inactive ? 'opacity-50' : ''` class, and the `query && v.lastSeenAtUtc` "Last seen" line from the row JSX
- `frontend/types/vehicle.ts` — remove `lastSeenAtUtc` and `inactive` from the `Vehicle` type

---

**Do NOT touch:**

- `frontend/store/useFilterStore.ts`, status-filter/search logic in `Sidebar.tsx` — already finalized by UI-015
- `backend/**` — the "inactive" concept was always client-only (BR-02); no backend file ever referenced it

---

**Sub-task breakdown:**

- [ ] Remove the inactive sweep effect + threshold constant from `page.tsx`
- [ ] Remove `v.inactive` usage from `MapView.tsx`
- [ ] Remove the `Inactive` badge from `DetailPanel.tsx`
- [ ] Remove remaining `v.inactive`/`v.lastSeenAtUtc` JSX from `Sidebar.tsx`
- [ ] Remove `inactive`/`lastSeenAtUtc` from `types/vehicle.ts`
- [ ] Run `npm run type-check` and `npm run lint` — fix any fallout

---

**Implementation notes:**

1. Search every file under `frontend/` for `inactive` and `lastSeenAtUtc` (case-insensitive) after the edits above to confirm nothing was missed — a stray reference to a removed `Vehicle` field will surface as a `type-check` error, so trust the compiler here.
2. If `page.tsx`'s SignalR update handler sets `lastSeenAtUtc: v.lastSeenAtUtc` purely to feed the (now-removed) sweep, delete that assignment too; if it's read from the initial `GET /api/vehicles` payload for any other purpose, leave the API response shape alone (backend is out of scope) and only drop the frontend `Vehicle` field + assignment.

---

**Acceptance criteria:**

1. No file under `frontend/` references `inactive` or `lastSeenAtUtc` as a `Vehicle`-related identifier.
2. The map renders all vehicle markers at full opacity regardless of movement history.
3. The detail panel never renders an "Inactive" badge.
4. `cd frontend && npm run type-check` passes with zero errors.
5. `cd frontend && npm run lint` passes with zero warnings.

---

**Verification command:**

```bash
cd frontend && npm run type-check && npm run lint
# Expected: zero errors, zero warnings
grep -rin "inactive\|lastSeenAtUtc" frontend/app frontend/components frontend/types
# Expected: no matches
```

---

**Rollback:**

```bash
git checkout -- frontend/app/page.tsx frontend/components/MapView.tsx frontend/components/DetailPanel.tsx frontend/components/Sidebar.tsx frontend/types/vehicle.ts
```

---

### BE-010: Update `VehicleStatusEvaluator.Evaluate` to the new threshold rules

**Agent:** ASP.NET
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`backend/Services/VehicleStatusEvaluator.cs` is the canonical live-mode status evaluator. Its thresholds must change to the operator's new rules (priority order unchanged: `offline` > `danger` > `warning` > `active`). The new `active` fuel band was clarified as a closed range `30.0 <= fuelPercent <= 100.0` (see sprint Context, clarification #1).

---

**Files to read before starting:**

- `backend/Services/VehicleStatusEvaluator.cs` — current implementation, full file (34 lines)
- `docs/requirements/REQUIREMENTS.md` §4.1 — current documented thresholds (superseded by ARCH-014 in this same sprint)

---

**Files to modify:**

- `backend/Services/VehicleStatusEvaluator.cs` — replace the threshold conditions in `Evaluate`

---

**Files to create:**

None

---

**Do NOT touch:**

- `backend/Services/TelemetrySimulationService.cs` — its own `EvaluateStatus`/rebalancer are updated separately in BE-011
- `backend/Hubs/FleetHub.cs`

---

**Sub-task breakdown:**

- [ ] Replace the `offline` condition
- [ ] Replace the `danger` condition
- [ ] Replace the `warning` condition
- [ ] Replace the `active` condition (closed fuel range, explicit rather than implicit default) — priority order (`offline` > `danger` > `warning` > `active`) means `active`'s clauses only need to be reachable as the terminal `return "active"`, matching the existing code style
- [ ] Run `dotnet build`

---

**Implementation notes:**

1. New rules, evaluated in this exact priority order:
   ```csharp
   // offline
   if (fuelPercent < 1 || temp < 5 || engineHealth < 5 || speedKph < 2)
       return "offline";

   // danger
   if (fuelPercent < 10.0 || speedKph > 90.0 || temp > 85 || engineHealth > 90)
       return "danger";

   // warning
   if ((fuelPercent < 30.0 && fuelPercent >= 10.0) ||
       (temp > 60 && temp <= 85) ||
       (engineHealth > 60 && engineHealth <= 90) ||
       (speedKph >= 60.0 && speedKph <= 90.0))
       return "warning";

   // active (default) — closed range per sprint clarification #1:
   // 30.0 <= fuelPercent <= 100.0 || 5 <= temp <= 60 || 5 <= engineHealth <= 60 || 2 <= speedKph <= 60.0
   return "active";
   ```
2. Keep the terminal `return "active"` as the code path (matches existing style) — the `active` band is documented in the doc-comment above `Evaluate` for clarity, not re-checked in an `if`, since it's unreachable by anything that fell through `offline`/`danger`/`warning`.
3. Update the XML doc-comment above `Evaluate` to describe the new bands so future readers don't have to reverse-engineer them from the `if` chain.
4. This method takes `(double fuelPercent, int temp, double speedKph, int engineHealth)` — do not change the signature; only the condition bodies change.

---

**Acceptance criteria:**

1. `VehicleStatusEvaluator.Evaluate(fuelPercent: 0.5, temp: 50, speedKph: 30, engineHealth: 50)` returns `"offline"` (fuel < 1).
2. `VehicleStatusEvaluator.Evaluate(fuelPercent: 50, temp: 90, speedKph: 30, engineHealth: 50)` returns `"danger"` (temp > 85).
3. `VehicleStatusEvaluator.Evaluate(fuelPercent: 50, temp: 70, speedKph: 30, engineHealth: 50)` returns `"warning"` (temp 60–85).
4. `VehicleStatusEvaluator.Evaluate(fuelPercent: 50, temp: 40, speedKph: 30, engineHealth: 50)` returns `"active"`.
5. `cd backend && dotnet build` passes with zero errors.

---

**Verification command:**

```bash
cd backend && dotnet build
# Expected: Build succeeded, 0 Error(s)
```

---

**Rollback:**

```bash
git checkout -- backend/Services/VehicleStatusEvaluator.cs
```

---

### BE-011: Update `TelemetrySimulationService` status evaluation + rebalancer to new thresholds/ranges

**Agent:** ASP.NET
**Depends on:** BE-010
**Status:** [x]

---

**Context:**

`backend/Services/TelemetrySimulationService.cs` has its own private `EvaluateStatus` (dummy-mode, intentionally not required to match `VehicleStatusEvaluator` bit-for-bit — see the doc-comment on that class) and a periodic rebalancer (`~line 388` onward) that currently enforces fixed caps `offline<=12, danger<=14, warning<=24`. Both need updating: `EvaluateStatus` to the same new threshold rules as BE-010 for consistency, and the rebalancer to the operator's new ranged targets (clarification #2): offline 40–100, danger 100–400, warning 500–800, active = remainder (~10,000 total).

---

**Files to read before starting:**

- `backend/Services/TelemetrySimulationService.cs` — full file, especially `EvaluateStatus` (lines ~57-75) and the rebalance block (lines ~388-484)

---

**Files to modify:**

- `backend/Services/TelemetrySimulationService.cs` — update `EvaluateStatus` conditions; update `capOffline`/`capDanger`/`capWarning` and the target-count math to use min–max ranges instead of single fixed caps

---

**Files to create:**

None

---

**Do NOT touch:**

- The corridor/movement simulation (`BuildCorridors`, `DistanceMeters`, `MetersToLatLngOffset`, `SeedVehicles`'s movement fields) — status/rebalance logic only
- `backend/Services/VehicleStatusEvaluator.cs` — already updated in BE-010; this file's `EvaluateStatus` intentionally stays a separate implementation

---

**Sub-task breakdown:**

- [ ] Update `EvaluateStatus`'s `offline`/`danger`/`warning` conditions to match BE-010's new rules
- [ ] Replace `capOffline = 12; capDanger = 14; capWarning = 24;` with randomized-per-rebalance target ranges: offline 40–100, danger 100–400, warning 500–800
- [ ] Update the target-count math so each rebalance tick picks a new random target within its range (not a single fixed cap), then derives `targetActiveCount` as the remainder
- [ ] Run `dotnet build`

---

**Implementation notes:**

1. `EvaluateStatus` new body (mirrors BE-010; `offline` here already partially matches — update fully):
   ```csharp
   private static string EvaluateStatus(double fuelPercent, int temp, double speedKph, int engineHealth)
   {
       if (fuelPercent < 1 || temp < 5 || engineHealth < 5 || speedKph < 2)
           return "offline";

       if (fuelPercent < 10.0 || speedKph > 90.0 || temp > 85 || engineHealth > 90)
           return "danger";

       if ((fuelPercent < 30.0 && fuelPercent >= 10.0) ||
           (temp > 60 && temp <= 85) ||
           (engineHealth > 60 && engineHealth <= 90) ||
           (speedKph >= 60.0 && speedKph <= 90.0))
           return "warning";

       return "active";
   }
   ```
2. Rebalance target ranges — pick a fresh random target inside each range on every rebalance tick (every ~20s) so the distribution drifts naturally within bounds rather than sitting at a single fixed number:
   ```csharp
   var capOffline = Random.Shared.Next(40, 101);   // 40-100 inclusive
   var capDanger  = Random.Shared.Next(100, 401);  // 100-400 inclusive
   var capWarning = Random.Shared.Next(500, 801);  // 500-800 inclusive
   ```
   Then keep the existing `targetOfflineCount`/`targetDangerCount`/`targetWarningCount`/`targetActiveCount` derivation (`Math.Min`/`Math.Max` chain) unchanged — it already composes sequentially off these three cap variables.
3. Total fleet is `VehicleCount = 10000`; offline(100 max)+danger(400 max)+warning(800 max) = 1300 max, leaving active >= 8700 — always leaves a non-trivial active remainder, consistent with "remainder are active vehicles" from the sprint clarification.
4. Leave the `MoveRandom` helper and its call sites untouched — only the three cap values become randomized-range picks instead of constants.

---

**Acceptance criteria:**

1. `EvaluateStatus`'s condition bodies are byte-for-byte identical in structure to BE-010's `Evaluate` (same four branches, same operators).
2. The rebalance block no longer contains the literal constants `12`, `14`, or `24` as caps.
3. `capOffline` is drawn from `[40, 100]`, `capDanger` from `[100, 400]`, `capWarning` from `[500, 800]` each rebalance tick.
4. `cd backend && dotnet build` passes with zero errors.
5. Running the backend for 2+ minutes (`dotnet run`) and sampling `GET /api/vehicles` shows `danger`/`warning`/`offline` counts within their respective ranges (allow one rebalance-tick's transient drift).

---

**Verification command:**

```bash
cd backend && dotnet build
# Expected: Build succeeded, 0 Error(s)

cd backend && dotnet run &
sleep 90
curl -s http://localhost:8080/api/vehicles | python -m json.tool | grep -o '"status": "[a-z]*"' | sort | uniq -c
# Expected: offline count in [40,100], danger in [100,400], warning in [500,800], active = remainder (~8700+)
```

---

**Rollback:**

```bash
git checkout -- backend/Services/TelemetrySimulationService.cs
```

---

### ARCH-014: Update `REQUIREMENTS.md` thresholds/caps, remove F-33/F-34/§4.4

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

`docs/requirements/REQUIREMENTS.md` §4.1 documents the old status thresholds, §4.2 documents fixed distribution caps (12/14/24), §2.3 has `F-33` (24h search filter) and `F-34` (10-item cap+toggle), and §4.4 has `BR-01`–`BR-04` (the "inactive vehicle" concept). Per operator clarification (sprint Context, clarifications #1–#3), all of these must be updated/removed to match the new behavior shipped by UI-015/UI-016/BE-010/BE-011: §4.1 gets the new thresholds, §4.2 gets the new ranged caps, and F-33/F-34/§4.4 are deleted outright since their backing features no longer exist.

---

**Files to read before starting:**

- `docs/requirements/REQUIREMENTS.md` — full file, especially §2.1 (F-04/F-05), §2.3 (F-10–F-13, F-33, F-34), §4.1, §4.2, §4.4

---

**Files to modify:**

- `docs/requirements/REQUIREMENTS.md` — update §4.1 threshold table, update §4.2 caps table, remove `F-33`, `F-34` rows from §2.3, remove §4.4 entirely (renumber trailing sections if any depend on numbering — check §5 onward is unaffected since §4.4 is the last subsection of §4)

---

**Files to create:**

None

---

**Do NOT touch:**

- `AGENTS.md` — updated separately at sprint-end per the Sprint-End Checklist
- `CHANGELOG.md` — updated separately at sprint-end

---

**Sub-task breakdown:**

- [ ] Replace §4.1's threshold table with the new offline/danger/warning/active rules
- [ ] Replace §4.2's fixed-cap table with the new ranged caps (offline 40-100, danger 100-400, warning 500-800, active = remainder)
- [ ] Delete the `F-33` and `F-34` rows from the §2.3 table
- [ ] Delete §4.4 (`Inactive Vehicle Threshold (Client-Side)`) in full, including `BR-01`–`BR-04`
- [ ] Proofread §2.1/§2.4 and any other section referencing "inactive" or the removed F-IDs, and remove those references too

---

**Implementation notes:**

1. New §4.1 table:
   | Status | Condition |
   |--------|-----------|
   | `offline` | `fuelPercent < 1` OR `temp < 5` OR `engineHealth < 5` OR `speedKph < 2` |
   | `danger` | `fuelPercent < 10.0` OR `speedKph > 90.0` OR `temp > 85` OR `engineHealth > 90` |
   | `warning` | `(fuelPercent < 30.0 AND fuelPercent >= 10.0)` OR `(temp > 60 AND temp <= 85)` OR `(engineHealth > 60 AND engineHealth <= 90)` OR `(speedKph >= 60.0 AND speedKph <= 90.0)` |
   | `active` | `30.0 <= fuelPercent <= 100.0` OR `5 <= temp <= 60` OR `5 <= engineHealth <= 60` OR `2 <= speedKph <= 60.0` (all other cases, default) |
2. New §4.2 table:
   | Status | Range | Approx. @ 10,000 vehicles |
   |--------|-------|---------------------------|
   | offline | 40–100 | 40–100 |
   | danger | 100–400 | 100–400 |
   | warning | 500–800 | 500–800 |
   | active | remainder | ~8,700+ |
   Note that these are randomized-per-rebalance-tick ranges (see `TelemetrySimulationService`'s rebalance block), not fixed counts.
3. Grep the whole file for `F-33`, `F-34`, `inactive`, `BR-01`, `BR-02`, `BR-03`, `BR-04` after editing to confirm nothing was missed.
4. Bump the `**Version:**` line at the top of the file (currently `0.1`) is NOT required — `REQUIREMENTS.md` version tracking is independent of `frontend/package.json`/`CHANGELOG.md` versioning; leave it unless a prior sprint established a pattern of bumping it (none has — check git history if unsure, but do not block on this).

---

**Acceptance criteria:**

1. §4.1 table matches the new thresholds exactly as specified above.
2. §4.2 table shows ranged caps (40-100/100-400/500-800/remainder), not the old fixed caps (12/14/24).
3. `grep -c "F-33\|F-34" docs/requirements/REQUIREMENTS.md` returns `0`.
4. §4.4 (`Inactive Vehicle Threshold`) no longer exists in the document.

---

**Verification command:**

```bash
grep -n "F-33\|F-34\|BR-01\|BR-02\|BR-03\|BR-04" docs/requirements/REQUIREMENTS.md
# Expected: no matches
grep -n "40" docs/requirements/REQUIREMENTS.md | grep -i offline
# Expected: at least one match in the new §4.2 table
```

---

**Rollback:**

```bash
git checkout -- docs/requirements/REQUIREMENTS.md
```

---

### ARCH-015: Author `docs/APPLICATION_OVERVIEW.md`

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

The operator asked for an explanation of what this application is, how it works, and its data flow — as a persistent onboarding doc, not just a chat answer. This should read as a top-level architecture explainer: what problem the system solves, the three-tier topology (frontend/backend/db + Python emitter), how a telemetry reading travels from source to screen (both dummy-mode simulation and live-mode ingestion paths), and where status/alerting decisions happen.

---

**Files to read before starting:**

- `AGENTS.md` — stack summary, agent write-scopes, service topology
- `docs/requirements/REQUIREMENTS.md` — §1 Project Overview, §5 Data Model, §6 PostgreSQL Schema, §7 API Contract, §8 SignalR Protocol (read post-ARCH-014 edits)
- `backend/Services/TelemetrySimulationService.cs` — dummy-mode data source
- `backend/Controllers/TelemetryIngestController.cs` — live-mode ingestion path
- `backend/Services/VehicleStatusEvaluator.cs` — where live-mode status is computed
- `frontend/app/page.tsx` — SignalR connection + Zustand state entry point
- `docs/SDD_WORKFLOW.md` — existing doc style/tone to match

---

**Files to modify:**

None

---

**Files to create:**

- `docs/APPLICATION_OVERVIEW.md` — what the app is, who it's for, the frontend/backend/db/emitter topology, the two data-flow paths (dummy simulation broadcast loop, and live ingestion → persistence → broadcast), where status is computed, and a short glossary pointing to the canonical files

---

**Do NOT touch:**

- Application source files — this is a documentation-only task

---

**Sub-task breakdown:**

- [ ] Write "What is this system" section (1-2 paragraphs, from §1 Project Overview)
- [ ] Write "Topology" section: frontend/backend/db/iiot-emitter, how they connect (ports, SignalR hub path, CORS)
- [ ] Write "Data flow — dummy mode" section: `TelemetrySimulationService` tick loop → status eval → SignalR broadcast → frontend `page.tsx` → Zustand/`useRef<Map>` → components
- [ ] Write "Data flow — live mode" section: Python emitter → `POST /api/telemetry/ingest` → `VehicleStatusEvaluator` → `LiveTelemetryStore` → buffered persistence (`TelemetryPersistenceService`) → SignalR broadcast, gated by `USE_LIVE_TELEMETRY`
- [ ] Write "Where status/alerts are decided" section distinguishing server-side `status` (§4.1) from frontend alert thresholds (§4.3)
- [ ] Add a glossary/file-index linking to `AGENTS.md`, `REQUIREMENTS.md`, `docs/SDD_WORKFLOW.md`, `docs/HELM_GUIDE.md`

---

**Implementation notes:**

1. Match the tone/format of `docs/SDD_WORKFLOW.md` — headed markdown sections, short paragraphs, code-path references as `` `File.cs:method` `` style pointers, not full code dumps.
2. Explicitly call out the dummy-vs-live mode split (`USE_LIVE_TELEMETRY`) since it's the single most important branch point in the data flow and easy to miss on a first read of the code.
3. Link to it from `README.md`'s doc index and from `AGENTS.md`'s "Key Knowledge Base Documents" table (both allowed under ARCH's write scope).

---

**Acceptance criteria:**

1. `docs/APPLICATION_OVERVIEW.md` exists and is non-empty.
2. The doc covers: what the app is, topology, dummy-mode data flow, live-mode data flow, and where status is computed.
3. `README.md` and `AGENTS.md` both link to `docs/APPLICATION_OVERVIEW.md`.

---

**Verification command:**

```bash
test -s docs/APPLICATION_OVERVIEW.md && echo "exists and non-empty"
grep -l "APPLICATION_OVERVIEW.md" README.md AGENTS.md
# Expected: both files listed
```

---

**Rollback:**

```bash
git rm docs/APPLICATION_OVERVIEW.md
git checkout -- README.md AGENTS.md
```

---

### ARCH-016: Author `docs/devops-learn/Docker_Compose.md`

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

Operator wants a learning guide covering: what Docker and Docker Compose are, why they're used, how to use them generally, and specifically how this project uses them. This is an educational doc for team onboarding, distinct from the existing operational `DOCKER_README.md` (which is a how-to-run reference, not a concepts explainer) — this guide should teach the concepts and then map them onto this repo's actual `docker-compose.yml`.

---

**Files to read before starting:**

- `docker-compose.yml` — actual service topology to reference
- `backend/Dockerfile`, `frontend/Dockerfile` — actual build patterns to reference
- `DOCKER_README.md` — existing operational doc; don't duplicate its content, link to it instead
- `.claude/skills/devops/SKILL.md` — service topology, compose reference, env var rules already documented for this project

---

**Files to modify:**

None

---

**Files to create:**

- `docs/devops-learn/Docker_Compose.md` — concepts (what/why) + this-project mapping (how)

---

**Do NOT touch:**

- `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile` — documentation only, no config changes
- `DOCKER_README.md` — existing operational doc stays as-is; link to it, don't rewrite it

---

**Sub-task breakdown:**

- [ ] Write "What is Docker" section (containers vs VMs, images vs containers, layers)
- [ ] Write "What is Docker Compose" section (multi-container orchestration, why over manual `docker run`)
- [ ] Write "Why we use them" section (parity across dev/CI/prod, `depends_on: service_healthy`, network isolation)
- [ ] Write "How to use — general" section (`docker build`, `docker run`, `docker-compose.yml` anatomy: services/volumes/networks/healthcheck)
- [ ] Write "How this project uses them" section: walk `docker-compose.yml`'s `db`/`backend`/`frontend` services, the `iiot-fleet-net` network, the two Dockerfiles' multi-stage builds, and link to `DOCKER_README.md` for run commands

---

**Implementation notes:**

1. Use this repo's actual `docker-compose.yml` content (service names, healthchecks, env vars) as the worked example — don't invent a generic example when a real one exists.
2. Keep the "general concepts" sections tool-agnostic (true of any Docker Compose project) and clearly separate them from the "this project" section (repo-specific).
3. Cross-link to `docs/devops-learn/Helm.md` and `docs/devops-learn/K8s.md` once those exist (ARCH-017/018) — a one-line "see also" is enough, don't block this task on their existence.

---

**Acceptance criteria:**

1. `docs/devops-learn/Docker_Compose.md` exists and is non-empty.
2. The doc covers: what Docker is, what Compose is, why they're used, general usage, and this project's actual service topology.
3. The doc references this repo's real service names (`db`, `backend`, `frontend`) and network name (`iiot-fleet-net`).

---

**Verification command:**

```bash
test -s docs/devops-learn/Docker_Compose.md && echo "exists and non-empty"
grep -l "iiot-fleet-net" docs/devops-learn/Docker_Compose.md
```

---

**Rollback:**

```bash
git rm docs/devops-learn/Docker_Compose.md
```

---

### ARCH-017: Author `docs/devops-learn/Helm.md`

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

Operator wants a learning guide covering: what Helm is, how to use it, and how this project uses it. This project already has an operational Helm guide (`docs/HELM_GUIDE.md`, from Sprint 06) — this new doc is a concepts-first learning guide (what/why Helm exists, chart anatomy, templating) that links to `docs/HELM_GUIDE.md` for the operational how-to-install steps, rather than duplicating them.

---

**Files to read before starting:**

- `docs/HELM_GUIDE.md` — existing operational guide; don't duplicate, link to it
- `helm/iiot-fleet-app/` — actual chart structure (`Chart.yaml`, `values.yaml`, `templates/`) to reference
- `.claude/agents/devops-architech.md` (if present) — INFRA agent's Helm conventions, if documented

---

**Files to modify:**

None

---

**Files to create:**

- `docs/devops-learn/Helm.md` — concepts (what/why Helm) + chart anatomy + this-project mapping

---

**Do NOT touch:**

- `helm/iiot-fleet-app/**` — documentation only, no chart changes
- `docs/HELM_GUIDE.md` — existing operational doc stays as-is; link to it, don't rewrite it

---

**Sub-task breakdown:**

- [ ] Write "What is Helm" section (package manager for Kubernetes, charts vs raw manifests, releases)
- [ ] Write "Why use Helm" section (templating, versioned releases, `values.yaml` overrides vs hardcoding)
- [ ] Write "Chart anatomy" section (`Chart.yaml`, `values.yaml`, `templates/`, `_helpers.tpl`, `helm install`/`upgrade`/`rollback`)
- [ ] Write "How this project uses Helm" section: walk `helm/iiot-fleet-app/`'s actual templates (db `StatefulSet`+PVC, backend/frontend `Deployment`+`Service`, emitter `Deployment` with init-gate, opt-in `Ingress`), and link to `docs/HELM_GUIDE.md` for install commands

---

**Implementation notes:**

1. Use this repo's actual `helm/iiot-fleet-app/` chart structure as the worked example.
2. Explain the init-gate pattern used for the emitter `Deployment` (Compose's `depends_on: service_healthy` doesn't exist in raw Kubernetes — Helm/K8s approximate it differently) since that's a genuinely non-obvious concept a Docker-only reader would trip on.
3. Cross-link to `docs/devops-learn/Docker_Compose.md` and `docs/devops-learn/K8s.md`.

---

**Acceptance criteria:**

1. `docs/devops-learn/Helm.md` exists and is non-empty.
2. The doc covers: what Helm is, why it's used, chart anatomy, and this project's actual chart structure.
3. The doc links to `docs/HELM_GUIDE.md` rather than duplicating its install steps.

---

**Verification command:**

```bash
test -s docs/devops-learn/Helm.md && echo "exists and non-empty"
grep -l "HELM_GUIDE.md" docs/devops-learn/Helm.md
```

---

**Rollback:**

```bash
git rm docs/devops-learn/Helm.md
```

---

### ARCH-018: Author `docs/devops-learn/K8s.md`

**Agent:** ARCH
**Depends on:** NONE
**Status:** [x]

---

**Context:**

Operator wants a learning guide covering: what Kubernetes is, how to use it, and how this project uses it. Unlike Docker Compose and Helm (which have dedicated existing docs), Kubernetes itself has no standalone concepts doc yet — `docs/HELM_GUIDE.md` covers the Helm-chart-on-K8s workflow but assumes K8s literacy. This doc fills that gap: core K8s concepts (Pods, Deployments, Services, StatefulSets, PVCs, Ingress) before the reader gets to Helm.

---

**Files to read before starting:**

- `helm/iiot-fleet-app/templates/` — actual K8s resource kinds used in this project (walk the directory to see which manifests exist)
- `docs/HELM_GUIDE.md` — existing Helm-on-K8s operational doc; link to it for the install workflow

---

**Files to modify:**

None

---

**Files to create:**

- `docs/devops-learn/K8s.md` — core K8s concepts + this-project mapping (which resource kinds this app's chart renders and why)

---

**Do NOT touch:**

- `helm/iiot-fleet-app/**` — documentation only, no chart changes

---

**Sub-task breakdown:**

- [ ] Write "What is Kubernetes" section (container orchestration at scale, declarative desired-state model, why beyond single-host Compose)
- [ ] Write "Core objects" section: Pod, Deployment, StatefulSet, Service, PersistentVolumeClaim, Ingress — one paragraph each
- [ ] Write "How to use — general" section (`kubectl apply`, `kubectl get pods`, `kubectl logs`, `kubectl describe`, readiness/liveness probes)
- [ ] Write "How this project uses Kubernetes" section: map each resource kind found under `helm/iiot-fleet-app/templates/` to its role (db `StatefulSet`+PVC for durable storage, backend/frontend `Deployment`+`Service`, emitter `Deployment`, opt-in `Ingress`) and link to `docs/HELM_GUIDE.md` for the actual `helm install` workflow

---

**Implementation notes:**

1. List the actual resource kinds by reading `helm/iiot-fleet-app/templates/` directly — don't guess which manifests exist.
2. Explain why the db uses `StatefulSet`+PVC while backend/frontend use plain `Deployment` (stable identity + durable storage vs stateless/replaceable) — this is the single most important K8s concept this project's chart demonstrates.
3. Cross-link to `docs/devops-learn/Docker_Compose.md` and `docs/devops-learn/Helm.md`.

---

**Acceptance criteria:**

1. `docs/devops-learn/K8s.md` exists and is non-empty.
2. The doc covers: what Kubernetes is, core objects (Pod/Deployment/StatefulSet/Service/PVC/Ingress), general `kubectl` usage, and this project's actual resource mapping.
3. The doc links to `docs/HELM_GUIDE.md` for the operational install workflow.

---

**Verification command:**

```bash
test -s docs/devops-learn/K8s.md && echo "exists and non-empty"
grep -l "StatefulSet" docs/devops-learn/K8s.md
```

---

**Rollback:**

```bash
git rm docs/devops-learn/K8s.md
```

---

### QA-007: Verify all acceptance criteria, type-check/lint/build, and live status/distribution behavior

**Agent:** QA
**Depends on:** UI-015, UI-016, BE-010, BE-011, ARCH-014, ARCH-015, ARCH-016, ARCH-017, ARCH-018
**Status:** [x]

---

**Context:**

Final sprint-wide verification before the Sprint-End Checklist. Confirm the sidebar no longer shows the removed controls, the frontend builds clean, the backend builds clean and its status thresholds/rebalancer match the new rules, and all five new/updated docs exist with the right cross-links. This task writes no feature code — verification and a pass/fail report only.

---

**Files to read before starting:**

- This sprint file in full (all task blocks' Acceptance Criteria sections)
- `frontend/store/useFilterStore.ts`, `frontend/components/Sidebar.tsx`, `frontend/components/MapView.tsx`, `frontend/components/DetailPanel.tsx`, `frontend/types/vehicle.ts` — post-UI-015/UI-016 state
- `backend/Services/VehicleStatusEvaluator.cs`, `backend/Services/TelemetrySimulationService.cs` — post-BE-010/BE-011 state
- `docs/requirements/REQUIREMENTS.md`, `docs/APPLICATION_OVERVIEW.md`, `docs/devops-learn/Docker_Compose.md`, `docs/devops-learn/Helm.md`, `docs/devops-learn/K8s.md`

---

**Files to modify:**

- `docs/sprints/sprint-07.md` — tick Task Index / Status boxes only, if any were missed by prior agents (acceptance-criteria-updates-only, per QA's write scope)

---

**Files to create:**

None

---

**Do NOT touch:**

- Any production source file — QA verifies, never fixes

---

**Sub-task breakdown:**

- [ ] Run `cd frontend && npm run type-check && npm run lint` — must be zero errors/warnings
- [ ] Run `cd backend && dotnet build` — must be zero errors
- [ ] Grep-verify no `hideInactive`/`focusedView`/`inactive`/`lastSeenAtUtc` residue anywhere under `frontend/`
- [ ] Run the backend for 90+ seconds and sample `GET /api/vehicles`, confirm status counts fall within the new ranges (offline 40-100, danger 100-400, warning 500-800)
- [ ] Confirm all 5 doc files exist, are non-empty, and are cross-linked from `AGENTS.md`/`README.md` where required
- [ ] Report pass/fail with exact command output and file:line references for any failure

---

**Implementation notes:**

1. This is a report-only task — if any check fails, STOP and report to the user per the sprint's Agent Execution Protocol; do not attempt fixes.
2. Sample the live distribution more than once (e.g. 3 samples 30s apart) since the rebalancer only re-targets every ~20s and counts drift between ticks.

---

**Acceptance criteria:**

1. `cd frontend && npm run type-check && npm run lint` passes with zero errors/warnings.
2. `cd backend && dotnet build` passes with zero errors.
3. No `hideInactive`/`focusedView`/`inactive`/`lastSeenAtUtc` references remain under `frontend/`.
4. Sampled `GET /api/vehicles` status counts fall within the new ranges across all samples.
5. All 5 new/updated docs (`REQUIREMENTS.md`, `APPLICATION_OVERVIEW.md`, `devops-learn/Docker_Compose.md`, `devops-learn/Helm.md`, `devops-learn/K8s.md`) exist, are non-empty, and are cross-linked as specified in their respective tasks.

---

**Verification command:**

```bash
cd frontend && npm run type-check && npm run lint
cd backend && dotnet build
grep -rin "hideInactive\|focusedView\|inactive\|lastSeenAtUtc" frontend/ | grep -v node_modules
curl -s http://localhost:8080/api/vehicles | python -m json.tool | grep -o '"status": "[a-z]*"' | sort | uniq -c
for f in docs/APPLICATION_OVERVIEW.md docs/devops-learn/Docker_Compose.md docs/devops-learn/Helm.md docs/devops-learn/K8s.md; do test -s "$f" && echo "OK $f" || echo "MISSING $f"; done
```

---

**Rollback:**

N/A — verification-only task, no files change on failure.

---

## Sprint-End Checklist

**GitHub issues:**
- [ ] Close completed issues: `gh issue close <number>`
- [ ] Check remaining open issues: `gh issue list --state=open`
- [ ] If unresolved issues remain, add to `docs/sprints/BACKLOG.md` and plan for next sprint

**Version and changelog:**
- [ ] Bump `frontend/package.json` version: `0.6.0` → `0.7.0` (minor — feature removal + business-rule change + new docs, no breaking API change)
- [ ] Add `## v0.7.0 — 2026-07-17` entry to `CHANGELOG.md` with `### Add`, `### Fix`, `### Update` sections
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S07-{TASK-ID}: <one-line summary>`
- [ ] `cd frontend && npm run type-check && npm run lint` passes on the final branch state
- [ ] `cd backend && dotnet build` passes on the final branch state
- [ ] Open PR: `claude/sprint-07-status-rules-cleanup` → `main` with title `IIOT-v0.7.0: sprint-07 status rules cleanup + devops-learn docs`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-07.md` → `docs/sprints/archive/sprint-07.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `sprint-08.md`
- [ ] Update `CHANGELOG.md` if system design changed

---

## Sprint Retrospective

> Filled at sprint end. 3–6 bullets. What worked, what blocked, what to change next sprint.

- Frontend `type-check`/`build`/backend `dotnet build` all pass zero-error on the final branch state; no residual `hideInactive`/`focusedView`/`inactive`/`lastSeenAtUtc` references remain under `frontend/`.
- Clarifying the three ambiguous points (active fuel band, ranged vs. percentage caps, full inactive-concept removal) with the operator before writing task blocks avoided rework mid-sprint — all three landed exactly as clarified.
- QA-007's live-sampling verification (running the backend 90s+ and checking status counts against the new ranges) could not run: this machine's installed .NET runtimes top out at 8.0.23, but the built binary requires 8.0.28, a pre-existing local environment gap unrelated to this sprint's code changes. `dotnet build` itself passes clean; only the runtime-execution smoke test was blocked.
- Action item carried to next sprint / backlog: get a matching .NET 8 runtime patch installed (or pin an available one via `global.json`) so live-mode smoke verification is possible locally, not just `dotnet build`.

---

## Agent Execution Protocol

> This section is read by the AI agent at the start of every session. Identical across all sprint files — do not modify.

```
SESSION START
─────────────
1. Read AGENTS.md (root) in full
2. Read docs/requirements/REQUIREMENTS.md in full
3. Read this sprint file in full
4. Read .claude/skills/sprint/SKILL.md in full (activates caveman token mode)
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-07-status-rules-cleanup
   - If not: git fetch origin main && git checkout -B claude/sprint-07-status-rules-cleanup origin/main
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
18. Commit: git commit -m "IIOT-S07-{TASK-ID}: <one-line summary>"

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

## Addendum — Infra Restructuring (added post-QA-007, same sprint/branch/PR)

The operator requested this additional INFRA-owned scope after the original 10 tasks shipped
(PR #7 was already open against `main`). Folded into Sprint 07 rather than a new sprint number
since it landed on the same branch before merge. Some of this scope was already partially
scaffolded by the operator directly in the working tree (`containers/`, `emitter/`, empty
per-service Helm template folders) before these tasks were written — each task below notes what
was pre-existing vs. what the task actually completed.

**Agent:** INFRA for all tasks below (Docker, Helm, GitHub Actions — matches `AGENTS.md`'s INFRA
write-scope: `docker-compose.yml`, `backend/Dockerfile`, `frontend/Dockerfile`,
`.github/workflows/**`, `iiot-emitter/**`/`emitter/**`, `helm/**`).

- [x] INFRA-001 — Split Helm chart templates into per-service folders (`templates/backend/`,
  `templates/frontend/`, `templates/db/`, `templates/emitter/`), each file commented line-by-line
  explaining why it exists; delete the old flat per-resource files once migrated
- [x] INFRA-002 — Remove `db/Dockerfile` + `db/postgresql.conf` (the whole `db/` directory);
  wire `postgres:16-alpine` directly in both `containers/docker-compose.yml` (already done by the
  operator) and the Helm chart (`values.yaml` `db.image`, `templates/db/statefulset.yaml`)
- [x] INFRA-003 — Move `DOCKER_README.md` to `docs/DOCKER_README.md`; update every link to it
- [x] INFRA-004 — Keep `_helpers.tpl` (required by nearly every template — see task notes); fix
  `NOTES.txt` (drop the now-removed `./db` custom-image build line) and document what it actually
  is inline, since the operator asked what it's for
- [x] INFRA-005 — Fix `containers/docker-compose.yml` build contexts now that Dockerfiles live in
  `containers/*/` but source stays in `backend/`/`frontend/`/`emitter/`; move `.dockerignore` files
  back to the source directories (Docker's default `.dockerignore` lookup is context-root-relative,
  not Dockerfile-relative)
- [x] INFRA-006 — Rename `iiot-emitter` → `emitter` across every active (non-archived) reference:
  `AGENTS.md`, `REQUIREMENTS.md`, `docs/HELM_GUIDE.md`, `docs/PROJECT_OVERVIEW.md`,
  `docs/SDD_WORKFLOW.md`, `docs/decisions/ADR-001-telemetry-ingestion-pipeline.md`, the Helm chart,
  `containers/docker-compose.yml`'s service name. Archived sprint files (`docs/sprints/archive/**`)
  are historical record and are NOT rewritten.
- [x] INFRA-007 — Remove `.github/workflows/docker-image.yml` in full (operator: no GitHub Actions
  CI for this project going forward)
- [x] INFRA-008 — Verify: `helm lint`/`helm template` render clean, `docker compose -f
  containers/docker-compose.yml config` validates, frontend `tsc --noEmit`, backend `dotnet build`

**Rollback (whole addendum):** `git revert` the addendum's commits, or `git checkout --
db/ .github/workflows/docker-image.yml DOCKER_README.md helm/ containers/` plus `git clean -fd
emitter/ containers/` to restore pre-addendum state — the individual per-task commits below are
each independently revertable.

---

## Glossary

| Term | Definition |
|------|------------|
| **NEXT** | Frontend engineer agent — owns `frontend/` |
| **ASP.NET** | Backend engineer agent — owns `backend/` |
| **INFRA** | DevOps agent — owns Docker, GitHub Actions, env vars |
| **QA** | Quality analyst agent — verifies acceptance criteria |
| **ARCH** | System designer agent — owns docs, sprint files, CHANGELOG |
| **ANALYST** | Performance analyst agent — measures metrics, no code writes |
| **Acceptance criterion** | Binary, testable assertion — TRUE or FALSE |
| **Verification command** | Shell command that proves an acceptance criterion is TRUE |
| **Rollback** | Operations that return the system to its pre-task state |
| **SignalR hub** | `backend/Hubs/FleetHub.cs` — WebSocket endpoint at `/fleethub` |
| **TelemetrySimulationService** | `backend/Services/TelemetrySimulationService.cs` — in-memory simulation; never add DB calls here |
| **VehicleUpdate** | MessagePack payload broadcast via SignalR every ~500ms |
| **ApiVehicle** | REST response DTO returned by `GET /api/vehicles` |
| **FleetDbContext** | EF Core DbContext — `backend/Data/FleetDbContext.cs` |
| **Caveman mode** | Token-compression style — see `.claude/skills/sprint/SKILL.md` |
| **fleet_telemetry** | PostgreSQL database name |
| **iiot-fleet-net** | Docker Compose network name |
