# Spec-Driven Development (SDD) Workflow

This document explains the methodology this project uses to plan and execute every change:
Spec-Driven Development (SDD). It describes the **process** — where specs live, how a spec
becomes a sprint, how a sprint becomes executable tasks, and how work gets closed out. For the
product spec itself, see `docs/requirements/REQUIREMENTS.md`.

---

## What SDD means in this repo

Every change starts as a written requirement, not a conversation. Requirements are broken into
sprint-sized slices, each sprint is broken into self-contained tasks an AI agent can execute
without asking a clarifying question mid-task, and every task must pass a binary, scriptable
acceptance check before it's considered done. Nothing is "done" because it looks right — it's
done because a verification command said so.

---

## The loop

```
┌─────────────────────────┐
│ 1. SPEC                 │   docs/requirements/REQUIREMENTS.md
│    Functional / non-    │   Functional (F-NN), non-functional (NF-NN),
│    functional / business│   and business-rule (BR-NN) requirements.
│    requirements          │   Source of truth for WHAT the system does.
└───────────┬──────────────┘
            │ operator brief / new requirement
            ▼
┌─────────────────────────┐
│ 2. SPRINT AUTHORING     │   .claude/skills/sprint/SKILL.md (the `sprint` skill)
│    Spec slice → sprint  │   Copies docs/sprints/archive/TEMPLATE.md, fills every
│    file                 │   {{PLACEHOLDER}}, splits scope into 3-12 tasks with
│                          │   IDs, dependencies, and a dependency map.
└───────────┬──────────────┘
            │ docs/sprints/sprint-NN.md, committed to main
            ▼
┌─────────────────────────┐
│ 3. TASK EXECUTION       │   AGENTS.md's Sprint Loop
│    Agent reads task     │   Each task lists exactly which files to read, modify,
│    block, implements,   │   create, and never touch. Agent works top-to-bottom
│    verifies, commits    │   through the dependency map, one commit per task:
│                          │   IIOT-S{NN}-{TASK-ID}: <summary>
└───────────┬──────────────┘
            │ all task Status: [x]
            ▼
┌─────────────────────────┐
│ 4. VERIFICATION         │   Each task's own "Verification command", plus a
│    Acceptance criteria  │   dedicated QA-NNN task at the end of most sprints
│    proven TRUE          │   that re-runs every prior task's acceptance criteria
│                          │   end-to-end before sprint-end is allowed to run.
└───────────┬──────────────┘
            │ QA task confirms sprint works
            ▼
┌─────────────────────────┐
│ 5. SPRINT-END            │   Sprint-End Checklist (ARCH agent)
│    Changelog, version,  │   Bump frontend/package.json version, add a
│    archive               │   CHANGELOG.md entry, open PR to main, move the
│                          │   sprint file to docs/sprints/archive/, update
│                          │   AGENTS.md's Current Sprint pointer.
└───────────┬──────────────┘
            │
            └──────────────► back to step 1 for the next slice of spec
```

Nothing skips a step. A sprint file is never hand-edited mid-execution by a non-ARCH agent
(`AGENTS.md`'s File Contracts table), and a task is never marked `[x]` without its verification
command actually passing.

---

## Roles in the loop

| Role | Owns | Step(s) it drives |
|------|------|--------------------|
| **ARCH** | `docs/**`, `AGENTS.md`, `README.md`, `CHANGELOG.md` | Step 1 (spec updates), step 2 (sprint authoring), step 5 (sprint-end) |
| **NEXT** | `frontend/**` | Step 3 (frontend tasks) |
| **ASP.NET** | `backend/**` | Step 3 (backend tasks) |
| **INFRA** | `containers/**` (Dockerfiles + `docker-compose.yml`), `.env*`, `emitter/**`, `helm/**` | Step 3 (infra tasks) |
| **QA** | `frontend/**/*.test.*`, `backend/**/*Tests*`, `docs/sprints/**` (acceptance-criteria updates only) | Step 4 |
| **ANALYST** | reads only; no write scope | Ad hoc, performance/telemetry measurement outside the sprint loop |

See `AGENTS.md` for the full read/write scope table per role — this document only summarizes it
in the context of the SDD loop.

---

## Why this loop, not ad hoc changes

- **A task an agent can't execute without asking a question is not ready.** The sprint template
  forces every task block to name exact files, exact sub-steps, and an exact verification
  command before work starts — ambiguity is resolved at authoring time, not mid-implementation.
- **Verification is a command, not a claim.** Every acceptance criterion has a matching shell
  command in "Verification command" that either passes or doesn't. This is what lets an agent
  self-certify a task instead of asking a human to eyeball it.
- **The spec, not the sprint, is the durable source of truth.** `docs/requirements/REQUIREMENTS.md`
  outlives any individual sprint; sprint files are disposable execution plans that get archived
  once delivered (`docs/sprints/archive/sprint-NN.md`).

---

## How to start a new sprint

1. Invoke the `sprint` skill (`.claude/skills/sprint/SKILL.md`) with the new scope — an operator
   brief, a set of requirement IDs, or a bug report.
2. The skill reads `docs/sprints/archive/TEMPLATE.md`, `AGENTS.md`, and
   `docs/requirements/REQUIREMENTS.md`, then authors `docs/sprints/sprint-NN.md` with every
   `{{PLACEHOLDER}}` filled and no source files read at generation time (source reads happen at
   task *execution* time, per each task's own "Files to read before starting" list).
3. The new sprint file is committed to `main` directly (sprint authoring is an ARCH write, not an
   execution-branch write) — see `docs/sprints/archive/sprint-05.md` and this sprint's own
   authoring commit for the pattern.
4. `AGENTS.md`'s `## Current Sprint` section is updated to point at the new file.
5. Execution begins on a dedicated branch (`claude/sprint-NN-{title}`, cut from `origin/main`),
   following the Sprint Loop in `AGENTS.md` and this sprint file's own "Agent Execution Protocol"
   section, task by task, until the Sprint-End Checklist runs.

---

## Related documents

| Document | Role in the loop |
|----------|-------------------|
| `docs/requirements/REQUIREMENTS.md` | Step 1 — the spec |
| `docs/sprints/archive/TEMPLATE.md` | Step 2 — the sprint file template |
| `.claude/skills/sprint/SKILL.md` | Step 2 — sprint authoring protocol |
| `AGENTS.md` | Steps 2-5 — roles, write scopes, File Contracts, Sprint Loop |
| `docs/sprints/sprint-NN.md` (active) / `docs/sprints/archive/sprint-NN.md` (delivered) | Steps 3-5 — the executable plan |
| `docs/sprints/BACKLOG.md` | Carryover items not yet sliced into a sprint |
| `CHANGELOG.md` | Step 5 — the delivered-version record |
