# Sprint {{NN}} — {{SPRINT_TITLE}}

> **TEMPLATE FILE — DO NOT EXECUTE DIRECTLY.**
> Copy this file as `docs/sprints/sprint-NN.md`, fill every `{{PLACEHOLDER}}`, and remove all annotation blocks (lines starting with `>`).
> Every field is mandatory. An AI agent MUST be able to execute every task without asking a human a clarifying question.
> If a field cannot be filled, the sprint is not ready to start.
> Branch naming: `{{TITLE}}` is 2–4 hyphenated words describing the change. Examples: `swagger-ui-setup`, `postgresql-ef-core`, `signalr-message-pack`, `vehicle-detail-panel`.

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
| **Sprint ID** | S{{NN}} |
| **Branch** | `claude/sprint-{{NN}}-{{TITLE}}` |
| **Base branch** | `main` — cut new branch from `origin/main` |
| **PR target** | `main` |
| **Start date** | {{YYYY-MM-DD}} |
| **End date** | {{YYYY-MM-DD}} |
| **Goal** | {{ONE sentence — user-visible outcome when sprint succeeds}} |
| **Success metric** | {{How to verify success — specific command or observable behavior}} |
| **Target env** | Local (`http://localhost:3000` / `http://localhost:8080`) |
| **Agents involved** | {{List only agents needed: NEXT, ASP.NET, INFRA, QA, ARCH, ANALYST}} |
| **Token mode** | caveman (default `full`) — see `.claude/skills/sprint/SKILL.md` |

> **Goal writing rule:** Goal MUST describe a user-visible outcome, not an implementation task.
> BAD: "Refactor the controller". GOOD: "Operators can view live vehicle telemetry logs in the dashboard detail panel."
> **Branch naming rule:** `{{TITLE}}` is kebab-case, 2–4 words. Examples: `swagger-setup`, `db-seed-vehicles`, `map-vehicle-markers`.

---

## Context

> 2–4 sentences explaining WHY this sprint exists. What user problem does it solve? What business objective does it advance? What triggered it (analytics finding, user complaint, stakeholder request)? AI agents read this to make better implementation decisions — be specific.

{{CONTEXT_PARAGRAPH}}

**Related documents:**
- `docs/requirements/REQUIREMENTS.md` — requirements section most relevant to this sprint
- {{Any other relevant doc}}

---

## Branch Setup (run once before any task)

> The executing agent MUST run this block before starting Task 1. Always cut from `origin/main`.

```bash
git fetch origin main
git checkout -B claude/sprint-{{NN}}-{{TITLE}} origin/main
git status    # must be clean
```

---

## Pre-Flight Checklist

> Run these checks BEFORE touching any code. If any check fails: STOP and report — do not proceed.

> **Sprint generation rule:** When GENERATING this sprint file, read `AGENTS.md` + this template + the user prompt only. Do NOT read source files at generation time — source reads happen at task EXECUTION time per the "Files to read before starting" list inside each task.

**Branch:**
- [ ] Branch `claude/sprint-{{NN}}-{{TITLE}}` exists and is clean (`git status` shows no uncommitted changes)
- [ ] Branch was cut from `origin/main`

**Frontend (if this sprint touches `frontend/`):**
- [ ] `cd frontend && npm install` completes with no errors
- [ ] `cd frontend && npm run type-check` passes with **zero errors** on the unmodified codebase
- [ ] `cd frontend && npm run lint` passes with **zero warnings** on the unmodified codebase
- [ ] `http://localhost:3000` loads in browser with no console errors

**Backend (if this sprint touches `backend/`):**
- [ ] `cd backend && dotnet build` passes with **zero errors** on the unmodified codebase
- [ ] `http://localhost:8080/swagger` loads Swagger UI
- [ ] `curl http://localhost:8080/api/vehicles` returns HTTP 200 with JSON array

**Database (if this sprint touches DB schema):**
- [ ] PostgreSQL is running on port 5432
- [ ] Database `fleet_telemetry` exists: `psql -U postgres -c "\l" | grep fleet_telemetry`
- [ ] All existing migrations are applied: `dotnet ef migrations list` shows all `[applied]`

**Docs:**
- [ ] Root `AGENTS.md` read in full
- [ ] `frontend/AGENTS.md` read in full (if frontend touched)
- [ ] `backend/AGENTS.md` read in full (if backend touched)
- [ ] `docs/requirements/REQUIREMENTS.md` read in full
- [ ] Active sprint file (`docs/sprints/sprint-{{NN}}.md`) read in full
- [ ] `.claude/skills/sprint/SKILL.md` read in full

**Sprint-specific:**
- [ ] {{Any sprint-specific pre-condition — e.g. "Figma design for new panel shared", "API contract agreed with team"}}

---

## Task Index (Top-Level Todo)

> Single-source todo list. Each entry mirrors a task block below. Tick ONLY when the matching task block also has `Status: [x]`.

- [ ] {{TASK-001}} — {{one-line summary}}
- [ ] {{TASK-002}} — {{one-line summary}}
- [ ] {{TASK-003}} — {{one-line summary}}

---

## Dependency Map

> Draw execution order. Tasks with no dependencies run first (or in parallel). Arrows show what blocks what.

```
{{TASK-001}} (no deps)     {{TASK-002}} (no deps, parallel)
       ↓                           ↓
       +───────────────────────────+
                    ↓
             {{TASK-003}}
                    ↓
             {{TASK-004}}
```

---

## Tasks

> Tasks are executed top-to-bottom following the dependency map. Each task is fully self-contained.
> **Granularity rule:** One task = one PR diff < 400 lines. If a feature touches > 4 files or > 400 lines, split it.

---

### {{TASK-ID}}: {{Task Title}}

> Task ID format:
> - `UI-NNN` — Next.js component or page (`frontend/components/` or `frontend/app/`)
> - `API-NNN` — Next.js Route Handler (`frontend/app/api/`)
> - `BE-NNN` — ASP.NET Core controller, service, or hub (`backend/Controllers/`, `backend/Services/`, `backend/Hubs/`)
> - `DB-NNN` — EF Core entity, DbContext, or migration (`backend/Data/`)
> - `INFRA-NNN` — Docker, Docker Compose, GitHub Actions, env vars
> - `QA-NNN` — tests, type-checks, acceptance verification
> - `ARCH-NNN` — documentation, requirements, sprint files
>
> Title: imperative verb phrase. "Add X", "Fix Y", "Integrate Z". Not "Working on X" or "X improvement".

**Agent:** {{NEXT | ASP.NET | INFRA | QA | ARCH | ANALYST}}
**Depends on:** {{TASK-ID, TASK-ID | NONE}}
**Status:** [ ]

---

**Context:**

> 3–5 sentences. Current state of the code, what is wrong or missing, exactly what needs to change. Name specific files. An agent reading only this paragraph MUST understand what to do.

{{CONTEXT}}

---

**Files to read before starting:**

> Every file the agent MUST read before writing a single line of code. Include reason after `—`.

- `{{path/to/file.ext}}` — {{reason: what to look for in this file}}
- `{{path/to/file.ext}}` — {{reason}}

---

**Files to modify:**

> Only files that will be changed. Do not list files that are only read.

- `{{path/to/file.ext}}` — {{what changes}}

---

**Files to create:**

> Only new files. Write "None" if no new files.

- `{{path/to/newfile.ext}}` — {{purpose}}
- `backend/Data/Migrations/<timestamp>_<Name>/` — auto-generated by `dotnet ef migrations add <Name>` (if DB schema changes)

---

**Do NOT touch:**

> Files or directories that must not be modified to prevent scope creep or accidental breakage.

- `backend/Services/TelemetrySimulationService.cs` — in-memory simulation; never add DB or HTTP calls here
- `backend/Hubs/FleetHub.cs` — hub stays minimal; path `/fleethub` must not change
- `{{path/to/file.ext}}` — {{reason}}

---

**Sub-task breakdown:**

> 2–6 atomic steps. Each is a single, independently verifiable action (a file edit, a command run, a UI check).

- [ ] {{Sub-step 1 — atomic action}}
- [ ] {{Sub-step 2 — atomic action}}
- [ ] {{Sub-step 3 — atomic action}}

---

**Implementation notes:**

> Numbered, specific instructions bridging "what" and "how". Include code snippets, data shapes, edge cases.

1. {{Specific instruction with code example if needed}}
2. {{Edge case the agent must handle}}
3. {{Constraint not derivable from reading code alone}}

---

**Acceptance criteria:**

> Binary, testable assertions — TRUE or FALSE, no partial credit. Present tense: "The component renders X", not "X should render".

1. {{Specific, testable assertion}}
2. {{Specific, testable assertion}}
3. `cd frontend && npm run type-check` passes with zero errors *(required on ALL tasks touching `frontend/`)*
4. `cd frontend && npm run lint` passes with zero warnings *(required on ALL tasks touching `frontend/`)*
5. `cd backend && dotnet build` passes with zero errors *(required on ALL tasks touching `backend/`)*
6. `dotnet ef database update` applies cleanly with no errors *(required if DB schema changed)*

---

**Verification command:**

> Copy-pasteable shell commands that prove acceptance criteria are TRUE. Include expected output in comments.

```bash
# Frontend checks (if frontend touched)
cd frontend && npm run type-check && npm run lint
# Expected: zero errors, zero warnings

# Backend checks (if backend touched)
cd backend && dotnet build
# Expected: Build succeeded

# API check
curl -s http://localhost:8080/api/vehicles | python -m json.tool | head -20
# Expected: JSON array with vehicle objects

# DB check (if schema changed)
psql -U postgres -d fleet_telemetry -c "\dt"
# Expected: tables listed

# Browser check
# Open http://localhost:3000 — verify: {{what to see}}
```

---

**Rollback:**

> Exact steps to undo this task without the original developer present.

```bash
# Revert modified files
git checkout -- {{path/to/modified/file}}

# If DB migration was applied:
cd backend && dotnet ef database update <PreviousMigrationName>
# Then delete the migration files: backend/Data/Migrations/<timestamp>_<Name>/

# If new files were created:
git rm {{path/to/new/file}}
```

---

> Repeat the task block above for each task. Minimum 3 tasks, maximum 12 tasks per sprint. If more than 12 tasks are needed, split into two sprints.

---

## Sprint-End Checklist

> Run AFTER all task checkboxes above are `[x]`. ARCH agent's responsibility.

**GitHub issues:**
- [ ] Close completed issues: `gh issue close <number>`
- [ ] Check remaining open issues: `gh issue list --state=open`
- [ ] If unresolved issues remain, add to `docs/sprints/BACKLOG.md` and plan for next sprint

**Version and changelog:**
- [ ] Bump `frontend/package.json` version: `{{CURRENT}}` → `{{NEW}}` (patch/minor/major per scope)
- [ ] Add `## v{{NEW_VERSION}} — {{YYYY-MM-DD}}` entry to `CHANGELOG.md` with `### Add`, `### Fix`, `### Update` sections
- [ ] Confirm `CHANGELOG.md` top version matches `frontend/package.json` version

**Git and CI:**
- [ ] All task commits follow format: `IIOT-S{{NN}}-{{TASK-ID}}: <one-line summary>`
- [ ] `cd frontend && npm run type-check && npm run lint` passes on the final branch state
- [ ] `cd backend && dotnet build` passes on the final branch state
- [ ] Open PR: `claude/sprint-{{NN}}-{{TITLE}}` → `main` with title `IIOT-v{{NEW_VERSION}}: sprint-{{NN}} {{brief description}}`

**Wrap-up:**
- [ ] Move `docs/sprints/sprint-{{NN}}.md` → `docs/sprints/archive/sprint-{{NN}}.md`
- [ ] Update `AGENTS.md` `## Current Sprint` to point to `sprint-{{NN+1}}.md`
- [ ] Update `CHANGELOG.md` if system design changed

---

## Sprint Retrospective

> Filled at sprint end. 3–6 bullets. What worked, what blocked, what to change next sprint.

- {{Win 1}}
- {{Win 2}}
- {{Blocker or pain point}}
- {{Action item carried to next sprint}}

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
5. Confirm branch: git rev-parse --abbrev-ref HEAD returns claude/sprint-{{NN}}-{{TITLE}}
   - If not: git fetch origin main && git checkout -B claude/sprint-{{NN}}-{{TITLE}} origin/main
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
18. Commit: git commit -m "IIOT-S{{NN}}-{{TASK-ID}}: <one-line summary>"

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
