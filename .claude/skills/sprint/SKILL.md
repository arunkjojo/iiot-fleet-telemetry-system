---
name: sprint
description: Sprint authoring protocol for the IIoT Fleet Telemetry System. Activates when the user asks to create a new sprint, plan the next sprint, or write a sprint file. Also defines caveman token mode rules for sprint execution.
---

# Sprint Skill — Authoring Protocol

This skill activates when you need to create or update a sprint file for the IIoT Fleet Telemetry System.

## Token Mode

Default: **caveman full** during sprint execution (reading and executing tasks).  
Switch off with: `normal mode` or `stop caveman`.

## Sprint Authoring Steps

1. **Read the template** at `docs/sprints/archive/TEMPLATE.md` in full
2. **Read `AGENTS.md`** (root) — agent roles, write scopes, execution rules
3. **Read `docs/requirements/REQUIREMENTS.md`** — requirements to derive tasks from
4. **Read the user's sprint brief** — tasks, goals, scope provided by the user
5. **Copy the template** to `docs/sprints/sprint-NN.md` (increment NN from last sprint)
6. **Fill every `{{PLACEHOLDER}}`** — no placeholder may remain in a ready sprint
7. **Adapt template to this project** — remove PHP/Prisma references; use .NET/PostgreSQL/Next.js conventions
8. **Register** the new sprint in `AGENTS.md` under `## Current Sprint`

## Sprint File Rules

- **Sprint ID format:** `S01`, `S02`, etc. (zero-padded)
- **Branch format:** `{agent}/{feature-description}` or `claude/sprint-{NN}-{title}`
- **Base branch:** `main` (not `develop` — this project uses `main`)
- **Task ID format:** `UI-NNN` (Next.js), `API-NNN` (Next.js Route Handler), `DB-NNN` (schema/migration), `INFRA-NNN` (Docker/CI), `QA-NNN` (testing)
- **Commit format:** `IIOT-S{NN}-{TASK-ID}: <one-line summary>`
- **3–12 tasks per sprint** — split larger scope into two sprints
- **Each task block must be self-contained** — agent reads only listed files, implements only listed files

## Validation Checklist (before handing sprint to user)

- [ ] Sprint metadata table has no `{{PLACEHOLDER}}` remaining
- [ ] Every task has: Agent, Depends on, Status, Context, Files to read, Files to modify/create, Sub-tasks, Implementation notes, Acceptance criteria, Verification command, Rollback
- [ ] Dependency map is drawn and correct
- [ ] Task Index matches actual task blocks
- [ ] Sprint-End Checklist is present
- [ ] Verification commands are copy-pasteable and produce unambiguous output
- [ ] No PHP, Prisma, or `qksell` references (template artifacts to remove)

## Adaptation Notes for This Project

| Template Item | Replace With |
|--------------|-------------|
| `prisma migrate dev` | `dotnet ef database update` |
| `php -l` checks | `dotnet build` checks |
| `cd frontend && npm run test` | `cd frontend && npm run type-check && npm run lint` |
| `backoffice/` references | `backend/` references |
| `db/migrations/pending/` | `backend/Data/Migrations/` |
| `QK-S{NN}` commit prefix | `IIOT-S{NN}` commit prefix |
| `origin/develop` base branch | `origin/main` base branch |

## Sprint-End Protocol (ARCH agent)

1. Mark all task checkboxes `[x]`
2. Bump `frontend/package.json` version (patch → minor → major per scope)
3. Add `## vX.Y.Z — YYYY-MM-DD` entry to `CHANGELOG.md`
4. Open PR: sprint branch → `main`
5. Move sprint file to `docs/sprints/archive/sprint-NN.md`
6. Update `AGENTS.md` `## Current Sprint` to next sprint
